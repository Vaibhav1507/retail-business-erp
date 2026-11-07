using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Rbos.Api.Persistence;
using static Rbos.Api.Application.SqlHelpers;
using Rbos.Domain;

namespace Rbos.Api.Application;

public enum OpeningStockStatus { Posted, Replayed, Invalid, Conflict, NoBusiness }

public sealed record OpeningStockOutcome(OpeningStockStatus Status, Guid? DocumentId = null, decimal? Total = null, string? Error = null);

public sealed record PostOpeningStockCommand(
    Guid UserId, string IdempotencyKey, Guid LocationId, DateOnly EffectiveDate,
    IReadOnlyList<OpeningStockLineInput>? Lines, string CorrelationId);

/// <summary>
/// Posts opening stock in one transaction under tenant RLS. All quantities, values, WAC and journal
/// amounts are recomputed here from the validated inputs using Rbos.Domain; client totals are never accepted.
/// </summary>
public sealed class OpeningStockService(TenantDb db, ILogger<OpeningStockService> log)
{
    const string Operation = "opening-stock";

    public async Task<OpeningStockOutcome> PostAsync(PostOpeningStockCommand cmd, CancellationToken ct)
    {
        var key = cmd.IdempotencyKey ?? "";
        if (key.Length is < 8 or > 100) return Invalid("Idempotency-Key header (8-100 chars) required.");
        if (!OpeningStockPolicy.TryValidate(cmd.Lines, cmd.EffectiveDate, DateOnly.FromDateTime(DateTime.UtcNow), out var lines, out var error))
            return Invalid(error!);

        var hash = Hash(cmd, lines);
        var tid = await db.FindTenantAsync(cmd.UserId, ct);
        if (tid is null) return new(OpeningStockStatus.NoBusiness, Error: "No business set up.");
        var tenantId = tid.Value;

        try
        {
            await using var s = await db.BeginAsync(cmd.UserId, tenantId, ct);

            var prior = await FindPriorAsync(s, key, hash, ct);
            if (prior is not null) return prior;

            if (await Scalar<Guid?>(s, "select id from core.stock_location where id=@l", ct, ("l", cmd.LocationId)) is null) return Invalid("Unknown location.");
            if (await Scalar<Guid?>(s, "select id from inventory.opening_stock_document where location_id=@l and status='Posted'", ct, ("l", cmd.LocationId)) is not null)
                return new(OpeningStockStatus.Conflict, Error: "Opening stock is already posted for this location.");

            var ids = lines.Select(l => l.ProductId).ToArray();
            await using (var q = s.Command("select count(*) from catalog.product where id = any(@i) and is_active"))
            {
                q.Parameters.AddWithValue("i", ids);
                if ((long)(await q.ExecuteScalarAsync(ct))! != ids.Length) return Invalid("Unknown or inactive product.");
            }

            var total = OpeningStockPolicy.Total(lines);
            var journal = BalancedJournalPolicy.OpeningStock(total);
            var accountIds = new List<Guid>();
            foreach (var j in journal)
            {
                var id = await Scalar<Guid?>(s, "select id from accounting.account where code=@c", ct, ("c", j.AccountCode));
                if (id is null) return new(OpeningStockStatus.Conflict, Error: "Chart of accounts missing.");
                accountIds.Add(id.Value);
            }

            var doc = Guid.NewGuid(); var entry = Guid.NewGuid(); var audit = Guid.NewGuid();
            await Exec(s, ct, "insert into audit.audit_event(tenant_id,id,actor_user_id,action,entity_type,entity_id,summary,correlation_id) values (@t,@a,@u,'OpeningStockPosted','OpeningStockDocument',@d,@s,@c)",
                ("t", tenantId), ("a", audit), ("u", cmd.UserId), ("d", doc), ("s", string.Create(CultureInfo.InvariantCulture, $"{lines.Count} lines, total {total.Amount:0.00} INR")), ("c", cmd.CorrelationId));
            await Exec(s, ct, "insert into accounting.journal_entry(tenant_id,id,effective_date,source_type,source_id) values (@t,@e,@d,'OpeningStock',@s)",
                ("t", tenantId), ("e", entry), ("d", cmd.EffectiveDate), ("s", doc));
            for (var i = 0; i < journal.Count; i++)
                await Exec(s, ct, "insert into accounting.journal_line(tenant_id,id,entry_id,account_id,debit,credit) values (@t,@i,@e,@a,@d,@c)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("e", entry), ("a", accountIds[i]), ("d", journal[i].Debit), ("c", journal[i].Credit));
            await Exec(s, ct, "insert into inventory.opening_stock_document(tenant_id,id,location_id,effective_date,status,created_by,posted_at,journal_entry_id,audit_event_id) values (@t,@d,@l,@e,'Posted',@u,now(),@j,@a)",
                ("t", tenantId), ("d", doc), ("l", cmd.LocationId), ("e", cmd.EffectiveDate), ("u", cmd.UserId), ("j", entry), ("a", audit));

            foreach (var l in lines)
            {
                var value = l.Value.Amount;
                await Exec(s, ct, "insert into inventory.opening_stock_line(tenant_id,document_id,product_id,quantity,unit_cost) values (@t,@d,@p,@q,@c)",
                    ("t", tenantId), ("d", doc), ("p", l.ProductId), ("q", l.Quantity.Value), ("c", l.UnitCost.Value));
                await Exec(s, ct, "insert into inventory.stock_movement(tenant_id,id,product_id,location_id,quantity_delta,unit_cost,value_delta,effective_date,source_type,source_id) values (@t,@i,@p,@l,@q,@c,@v,@e,'OpeningStock',@d)",
                    ("t", tenantId), ("i", Guid.NewGuid()), ("p", l.ProductId), ("l", cmd.LocationId), ("q", l.Quantity.Value), ("c", l.UnitCost.Value), ("v", value), ("e", cmd.EffectiveDate), ("d", doc));
                await Exec(s, ct, "insert into inventory.stock_balance(tenant_id,product_id,location_id,quantity) values (@t,@p,@l,@q) on conflict (tenant_id,product_id,location_id) do update set quantity = inventory.stock_balance.quantity + excluded.quantity",
                    ("t", tenantId), ("p", l.ProductId), ("l", cmd.LocationId), ("q", l.Quantity.Value));

                var current = await ReadCostAsync(s, l.ProductId, ct);
                var next = WeightedAverageCostCalculator.ApplyReceipt(current, l.Quantity, l.UnitCost);
                await Exec(s, ct, "insert into inventory.item_cost(tenant_id,product_id,total_quantity,inventory_value,wac) values (@t,@p,@q,@v,@w) on conflict (tenant_id,product_id) do update set total_quantity=excluded.total_quantity, inventory_value=excluded.inventory_value, wac=excluded.wac",
                    ("t", tenantId), ("p", l.ProductId), ("q", next.TotalQuantity), ("v", next.InventoryValue), ("w", next.Wac));
            }

            await Exec(s, ct, "insert into platform.idempotency_record(tenant_id,operation,key,request_hash,result_document_id) values (@t,@o,@k,@h,@d)",
                ("t", tenantId), ("o", Operation), ("k", key), ("h", hash), ("d", doc));
            await s.CommitAsync(ct);
            return new(OpeningStockStatus.Posted, doc, total.Amount);
        }
        catch (PostgresException e) when (e.SqlState == "23505")
        {
            await using var s2 = await db.BeginAsync(cmd.UserId, tenantId, ct);
            return await FindPriorAsync(s2, key, hash, ct)
                ?? new(OpeningStockStatus.Conflict, Error: "Opening stock is already posted for this location.");
        }
        catch (PostgresException e)
        {
            log.LogWarning(e, "Opening stock posting rejected by the database ({SqlState}).", e.SqlState);
            return new(OpeningStockStatus.Conflict, Error: "Posting rejected.");
        }
    }

    static OpeningStockOutcome Invalid(string error) => new(OpeningStockStatus.Invalid, Error: error);

    static async Task<OpeningStockOutcome?> FindPriorAsync(TenantSession s, string key, string hash, CancellationToken ct)
    {
        await using var q = s.Command("select request_hash,result_document_id from platform.idempotency_record where operation=@o and key=@k");
        q.Parameters.AddWithValue("o", Operation);
        q.Parameters.AddWithValue("k", key);
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return r.GetString(0) == hash
            ? new(OpeningStockStatus.Replayed, r.GetGuid(1))
            : new(OpeningStockStatus.Conflict, Error: "Idempotency-Key reused with a different request.");
    }

    static async Task<CostState> ReadCostAsync(TenantSession s, Guid productId, CancellationToken ct)
    {
        await using var q = s.Command("select total_quantity,inventory_value,wac from inventory.item_cost where product_id=@p for update");
        q.Parameters.AddWithValue("p", productId);
        await using var r = await q.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new CostState(r.GetDecimal(0), r.GetDecimal(1), r.GetDecimal(2)) : CostState.Empty;
    }

    static string Hash(PostOpeningStockCommand c, IReadOnlyList<OpeningStockLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append(c.LocationId).Append('|').Append(c.EffectiveDate.ToString("O", CultureInfo.InvariantCulture));
        foreach (var l in lines.OrderBy(x => x.ProductId))
            sb.Append('|').Append(l.ProductId).Append(':').Append(l.Quantity.Value.ToString(CultureInfo.InvariantCulture)).Append(':').Append(l.UnitCost.Value.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
    }
