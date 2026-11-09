using System.Security.Cryptography;
using System.Text;
using Rbos.Api.Persistence;

namespace Rbos.Api.Application;

/// <summary>Small helpers shared by the SQL-based application services.</summary>
internal static class SqlHelpers
{
    public static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static string Hash(string v) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(v)));

    /// <summary>Allocates the next gapless invoice number for the tenant's financial year. Must run inside the sale transaction so a rollback releases the number.</summary>
    public static async Task<string> NextInvoiceNoAsync(TenantSession s, Guid tenantId, DateOnly saleDate, CancellationToken ct)
    {
        var startMonth = await Scalar<short?>(s, "select financial_year_start_month from core.business_profile where tenant_id=@t", ct, ("t", tenantId)) ?? 4;
        var fy = saleDate.Month >= startMonth ? saleDate.Year : saleDate.Year - 1;
        var n = await Scalar<int>(s, "insert into sales.invoice_counter(tenant_id,fy_start_year,last_number) values (@t,@y,1) on conflict (tenant_id,fy_start_year) do update set last_number = sales.invoice_counter.last_number + 1 returning last_number", ct, ("t", tenantId), ("y", fy));
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var label = startMonth == 1 ? fy.ToString(inv) : fy.ToString(inv) + "-" + ((fy + 1) % 100).ToString("00", inv);
        return "INV/" + label + "/" + n.ToString("000000", inv);
    }

    public static async Task Exec(TenantSession s, CancellationToken ct, string sql, params (string, object?)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        await c.ExecuteNonQueryAsync(ct);
    }

    public static async Task<T?> Scalar<T>(TenantSession s, string sql, CancellationToken ct, params (string, object?)[] ps)
    {
        await using var c = s.Command(sql);
        foreach (var (n, v) in ps) c.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var r = await c.ExecuteScalarAsync(ct);
        return r is null or DBNull ? default : (T)r;
    }

    /// <summary>Returns the earlier result for an idempotency key (replay), a 409 if the key was reused with different content, or null if the key is new. <paramref name="table"/> must be a trusted constant.</summary>
    public static async Task<ServiceResult<object>?> Prior(TenantSession s, string table, string key, string hash, CancellationToken ct)
    {
        await using var q = s.Command($"select id, request_hash from {table} where idempotency_key=@k");
        q.Parameters.AddWithValue("k", key);
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return r.GetString(1) == hash ? ServiceResult<object>.Ok(new { id = r.GetGuid(0), replayed = true }) : ServiceResult<object>.Fail("Idempotency-Key reused with a different request.", 409);
    }

    /// <summary>Validates and stores optional bill allocations for a payment. Returns an error message, or null when valid.</summary>
    public static async Task<string?> AllocateAsync(TenantSession s, Guid tenantId, string table, string billColumn, string openSql, Guid partyId, Guid paymentId, decimal payAmount, IReadOnlyList<BillAllocation>? allocations, CancellationToken ct)
    {
        if (allocations is null || allocations.Count == 0) return null;
        if (allocations.Select(a => a.BillId).Distinct().Count() != allocations.Count) return "A bill can appear only once in allocations.";
        if (allocations.Any(a => a.Amount <= 0 || decimal.Round(a.Amount, 2) != a.Amount)) return "Allocation amounts must be positive with at most 2 decimals.";
        if (allocations.Sum(a => a.Amount) > payAmount) return "Allocated amount exceeds the payment.";
        foreach (var a in allocations)
        {
            var open = await Scalar<decimal?>(s, openSql, ct, ("b", a.BillId), ("p", partyId));
            if (open is null) return "Unknown bill in allocations.";
            if (a.Amount > open) return "Allocation exceeds the bill's outstanding amount.";
            await Exec(s, ct, $"insert into {table}(tenant_id,id,payment_id,{billColumn},amount) values (@t,@i,@p,@b,@a)", ("t", tenantId), ("i", Guid.NewGuid()), ("p", paymentId), ("b", a.BillId), ("a", a.Amount));
        }
        return null;
    }
}

public sealed record BillAllocation(Guid BillId, decimal Amount);
