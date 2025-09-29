using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class AuditLogger : IAuditLogger
{
    private readonly string _connStr;
    private readonly IHttpContextAccessor _http;
    private readonly string _pepper;

    public AuditLogger(IConfiguration cfg, IHttpContextAccessor http)
    {
        _connStr = cfg.GetConnectionString("DefaultConnection") 
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
                   ?? "Data Source=becs.db";
        _http = http;
        _pepper = cfg["Audit:HashPepper"] ?? "CHANGE_ME";
    }

    public async Task<long> LogAsync(AuditEntry e, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var ctx = _http.HttpContext;
        // Enrich with request/user
        var correlationId = ctx.GetCorrelationId();
        var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
        var ua = ctx?.Request?.Headers["User-Agent"].ToString();
        var method = ctx?.Request?.Method;
        var path = ctx?.Request?.Path.Value;

        // Try enrich user if not supplied
        if (ctx?.User?.Identity?.IsAuthenticated == true)
        {
            e.UserId ??= ctx.User.Claims.FirstOrDefault(c => c.Type.Contains("nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value
                      ?? ctx.User.Identity?.Name;
            e.UserName ??= ctx.User.Identity?.Name;
        }

        // Get previous hash
        string? prevHash = null;
        await using (var con = new SqliteConnection(_connStr))
        {
            await con.OpenAsync(ct);

            await using (var cmdPrev = con.CreateCommand())
            {
                cmdPrev.CommandText = "SELECT hash FROM audit_logs ORDER BY id DESC LIMIT 1;";
                prevHash = (string?)await cmdPrev.ExecuteScalarAsync(ct);
            }

            var hash = ComputeHash(
                timestampUtc: now,
                userId: e.UserId,
                userName: e.UserName,
                actorType: e.ActorType,
                action: e.Action,
                entityName: e.EntityName,
                entityId: e.EntityId,
                detailsJson: e.DetailsJson,
                success: e.Success,
                correlationId: correlationId,
                ip: ip, ua: ua, method: method, path: path,
                prevHash: prevHash,
                pepper: _pepper
            );

            await using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                @"INSERT INTO audit_logs
                  (timestamp_utc, user_id, user_name, actor_type, action, entity_name, entity_id, details_json, success,
                   correlation_id, ip_address, user_agent, http_method, path, prev_hash, hash)
                  VALUES
                  ($ts,$uid,$un,$at,$ac,$en,$eid,$det,$ok,$cid,$ip,$ua,$hm,$pth,$prev,$hash);
                  SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$ts", now.ToUniversalTime().ToString("o"));
                cmd.Parameters.AddWithValue("$uid", (object?)e.UserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$un", (object?)e.UserName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$at", e.ActorType);
                cmd.Parameters.AddWithValue("$ac", e.Action);
                cmd.Parameters.AddWithValue("$en", (object?)e.EntityName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$eid", (object?)e.EntityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$det", (object?)e.DetailsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$ok", e.Success ? 1 : 0);
                cmd.Parameters.AddWithValue("$cid", (object?)correlationId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$ip", (object?)ip ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$ua", (object?)ua ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$hm", (object?)method ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$pth", (object?)path ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$prev", (object?)prevHash ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$hash", hash);

                var id = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
                return id;
            }
        }
    }

    private static string ComputeHash(
        DateTimeOffset timestampUtc,
        string? userId, string? userName,
        string actorType, string action,
        string? entityName, string? entityId,
        string? detailsJson, bool success,
        string? correlationId,
        string? ip, string? ua, string? method, string? path,
        string? prevHash, string pepper)
    {
        var obj = new
        {
            timestampUtc = timestampUtc.ToUniversalTime().ToString("o"),
            userId, userName, actorType, action, entityName, entityId,
            detailsJson, success, correlationId, ip, ua, method, path, prevHash, pepper
        };
        var json = JsonSerializer.Serialize(obj);
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
