using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Rbos.Api.Application;

public interface ISmsSender { Task SendAsync(string mobile, string code, CancellationToken ct); }

/// <summary>Development only: writes the OTP to the API log instead of sending an SMS.</summary>
public sealed class DevSmsSender(ILogger<DevSmsSender> log) : ISmsSender
{
    public Task SendAsync(string mobile, string code, CancellationToken ct)
    {
        log.LogWarning("DEV OTP for {Mobile}: {Code}", mobile, code);
        return Task.CompletedTask;
    }
}

/// <summary>MSG91 OTP API. Needs Sms:Provider=msg91, Sms:AuthKey and Sms:TemplateId (DLT-approved template) in user secrets.</summary>
public sealed class Msg91SmsSender(HttpClient http, IConfiguration cfg) : ISmsSender
{
    public async Task SendAsync(string mobile, string code, CancellationToken ct)
    {
        var template = cfg["Sms:TemplateId"] ?? throw new InvalidOperationException("Sms:TemplateId is not configured.");
        var url = $"https://control.msg91.com/api/v5/otp?template_id={Uri.EscapeDataString(template)}&mobile=91{mobile}&otp={code}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("authkey", cfg["Sms:AuthKey"]);
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"SMS provider returned {(int)res.StatusCode}.");
    }
}

public sealed record OtpSendResult(bool Ok, string? Error, string? DevCode = null);

public sealed class OtpService(NpgsqlDataSource ds, ISmsSender sms, IConfiguration cfg, IWebHostEnvironment env, ILogger<OtpService> log)
{
    public static readonly Regex MobileRe = new("^[6-9][0-9]{9}$");
    const int MaxAttempts = 5, MaxPerHour = 5;

    string Hash(Guid id, string mobile, string code)
    {
        var pepper = cfg["Otp:Pepper"];
        if (string.IsNullOrEmpty(pepper))
        {
            if (!env.IsDevelopment()) throw new InvalidOperationException("Otp:Pepper is not configured.");
            pepper = "dev-only-pepper";
        }
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes($"{id}|{mobile}|{code}")));
    }

    public async Task<OtpSendResult> SendAsync(string mobile, string purpose, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using (var q = new NpgsqlCommand("select count(*) filter (where created_at > now() - interval '60 seconds'), count(*) from identity.otp_challenge where mobile=@m and created_at > now() - interval '1 hour'", con))
        {
            q.Parameters.AddWithValue("m", mobile);
            await using var r = await q.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            if (r.GetInt64(0) > 0) return new(false, "Please wait a minute before asking for another code.");
            if (r.GetInt64(1) >= MaxPerHour) return new(false, "Too many codes requested. Try again later.");
        }
        var id = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await using (var i = new NpgsqlCommand("insert into identity.otp_challenge(id,mobile,purpose,code_hash,expires_at) values (@i,@m,@p,@h, now() + interval '5 minutes')", con))
        {
            i.Parameters.AddWithValue("i", id); i.Parameters.AddWithValue("m", mobile); i.Parameters.AddWithValue("p", purpose); i.Parameters.AddWithValue("h", Hash(id, mobile, code));
            await i.ExecuteNonQueryAsync(ct);
        }
        try { await sms.SendAsync(mobile, code, ct); }
        catch (Exception e)
        {
            log.LogError(e, "Failed to send OTP SMS.");
            return new(false, "Could not send the code. Please try again.");
        }
        return new(true, null, sms is DevSmsSender && env.IsDevelopment() ? code : null);
    }

    /// <summary>Checks the newest open code. For signup the challenge stays reusable as a proof until consumed; for other purposes it is consumed at once.</summary>
    public async Task<Guid?> VerifyAsync(string mobile, string purpose, string code, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        Guid id; string hash;
        await using (var u = new NpgsqlCommand(@"update identity.otp_challenge set attempts = attempts + 1 where id = (
            select id from identity.otp_challenge where mobile=@m and purpose=@p and verified_at is null and consumed_at is null and expires_at > now() and attempts < @max
            order by created_at desc limit 1) returning id, code_hash", con))
        {
            u.Parameters.AddWithValue("m", mobile); u.Parameters.AddWithValue("p", purpose); u.Parameters.AddWithValue("max", MaxAttempts);
            await using var r = await u.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            id = r.GetGuid(0); hash = r.GetString(1);
        }
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(id, mobile, code.Trim())), Encoding.ASCII.GetBytes(hash));
        if (!ok) return null;
        await using var v = new NpgsqlCommand("update identity.otp_challenge set verified_at = now(), consumed_at = case when @c then now() else null end where id=@i", con);
        v.Parameters.AddWithValue("c", purpose != "signup"); v.Parameters.AddWithValue("i", id);
        await v.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<bool> ProofValidAsync(Guid proof, string mobile, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var c = new NpgsqlCommand("select 1 from identity.otp_challenge where id=@i and mobile=@m and purpose='signup' and consumed_at is null and verified_at > now() - interval '30 minutes'", con);
        c.Parameters.AddWithValue("i", proof); c.Parameters.AddWithValue("m", mobile);
        return await c.ExecuteScalarAsync(ct) is not null;
    }

    public async Task<bool> ConsumeProofAsync(Guid proof, string mobile, CancellationToken ct)
    {
        await using var con = await ds.OpenConnectionAsync(ct);
        await using var c = new NpgsqlCommand("update identity.otp_challenge set consumed_at=now() where id=@i and mobile=@m and purpose='signup' and consumed_at is null and verified_at > now() - interval '30 minutes'", con);
        c.Parameters.AddWithValue("i", proof); c.Parameters.AddWithValue("m", mobile);
        return await c.ExecuteNonQueryAsync(ct) == 1;
    }
}
