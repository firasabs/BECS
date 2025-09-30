using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class AuditRow
{
    public long Id { get; set; }
    public string TimestampUtc { get; set; } = "";
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string ActorType { get; set; } = "User";
    public string Action { get; set; } = "";
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public bool Success { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? HttpMethod { get; set; }
    public string? Path { get; set; }
    public string? PrevHash { get; set; }
    public string? Hash { get; set; }
}

public class AuditController : Controller
{
    private readonly string _connStr;
    public AuditController(IConfiguration cfg)
    {
        // Prefer env var if set, else appsettings ConnectionStrings:DefaultConnection, else fallback
        _connStr =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? cfg.GetConnectionString("DefaultConnection")
            ?? "Data Source=becs.db";
    }

    // GET /Audit?search=&action=&entity=&corr=&page=1&pageSize=50
    [HttpGet("/Audit")]
    public async Task<IActionResult> Index(
        string? search, string? act, string? entity, string? corr,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var rows = new List<AuditRow>();
        long total = 0;

        await using var con = new SqliteConnection(_connStr);
        try
        {
            await con.OpenAsync(ct);

            // DEBUG INFO: does table exist?
            var tableExists = await TableExistsAsync(con, "audit_logs", ct);
            ViewBag.TableExists = tableExists;
            if (!tableExists)
            {
                ViewBag.Error = "Table 'audit_logs' not found in this database. Make sure you applied the Part 2 schema to the SAME DB file your app is using.";
                return View(rows);
            }

            // DEBUG INFO: total rows, even before applying filters
            ViewBag.TotalAll = await CountAllAsync(con, "audit_logs", ct);

            // Build WHERE
            var where = " WHERE 1=1";
            var parms = new List<(string, object?)>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (action LIKE $s OR details_json LIKE $s OR user_name LIKE $s OR entity_name LIKE $s OR entity_id LIKE $s)";
                parms.Add(("$s", $"%{search}%"));
            }
            if (!string.IsNullOrWhiteSpace(act))
            {
                where += " AND action LIKE $ac";
                parms.Add(("$ac", $"%{act}%"));
            }
            if (!string.IsNullOrWhiteSpace(entity))
            {
                where += " AND entity_name LIKE $en";
                parms.Add(("$en", $"%{entity}%"));
            }
            if (!string.IsNullOrWhiteSpace(corr))
            {
                where += " AND correlation_id = $cid";
                parms.Add(("$cid", corr));
            }

            // Count (with filters)
            await using (var cmdCount = con.CreateCommand())
            {
                cmdCount.CommandText = $"SELECT COUNT(1) FROM audit_logs {where};";
                foreach (var p in parms) cmdCount.Parameters.AddWithValue(p.Item1, p.Item2 ?? DBNull.Value);
                total = (long)(await cmdCount.ExecuteScalarAsync(ct) ?? 0L);
            }

            // Page
            var offset = (page - 1) * pageSize;
            await using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = $@"
                  SELECT id, timestamp_utc, user_id, user_name, actor_type, action, entity_name, entity_id, details_json, success,
                         correlation_id, ip_address, user_agent, http_method, path, prev_hash, hash
                  FROM audit_logs
                  {where}
                  ORDER BY id DESC
                  LIMIT $ps OFFSET $ofs;";
                foreach (var p in parms) cmd.Parameters.AddWithValue(p.Item1, p.Item2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$ps", pageSize);
                cmd.Parameters.AddWithValue("$ofs", offset);

                await using var rdr = await cmd.ExecuteReaderAsync(ct);
                while (await rdr.ReadAsync(ct))
                {
                    rows.Add(new AuditRow
                    {
                        Id = rdr.GetInt64(0),
                        TimestampUtc = rdr.GetString(1),
                        UserId = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                        UserName = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                        ActorType = rdr.GetString(4),
                        Action = rdr.GetString(5),
                        EntityName = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                        EntityId = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                        DetailsJson = rdr.IsDBNull(8) ? null : rdr.GetString(8),
                        Success = !rdr.IsDBNull(9) && rdr.GetInt64(9) == 1,
                        CorrelationId = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                        IpAddress = rdr.IsDBNull(11) ? null : rdr.GetString(11),
                        UserAgent = rdr.IsDBNull(12) ? null : rdr.GetString(12),
                        HttpMethod = rdr.IsDBNull(13) ? null : rdr.GetString(13),
                        Path = rdr.IsDBNull(14) ? null : rdr.GetString(14),
                        PrevHash = rdr.IsDBNull(15) ? null : rdr.GetString(15),
                        Hash = rdr.IsDBNull(16) ? null : rdr.GetString(16),
                    });
                }
            }

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.ActionFilter = act;
            ViewBag.Entity = entity;
            ViewBag.Corr = corr;

            if (rows.Count == 0 && (long)ViewBag.TotalAll > 0)
            {
                ViewBag.Info = "audit_logs has rows, but your current filter or page shows none. Try clearing filters or go to page 1.";
            }
        }
        catch (SqliteException ex)
        {
            // DEBUG: surface DB errors on the page (only for development)
            ViewBag.Error = $"SQLite error: {ex.SqliteErrorCode} — {ex.Message}";
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error: {ex.Message}";
        }

        return View(rows);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection con, string table, CancellationToken ct)
    {
        await using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", table);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj != null;
    }

    private static async Task<long> CountAllAsync(SqliteConnection con, string table, CancellationToken ct)
    {
        await using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(1) FROM {table};";
        var obj = await cmd.ExecuteScalarAsync(ct);
        return (long)(obj ?? 0L);
    }
}
