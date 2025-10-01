using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles="admin")] // hippa
public class ExportController : Controller
{
    private readonly string _connStr;
    private readonly IAuditLogger _audit;

    public ExportController(IConfiguration cfg, IAuditLogger audit)
    {
        _connStr = cfg.GetConnectionString("DefaultConnection") 
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
                   ?? "Data Source=becs.db";
        _audit = audit;
    }

    [HttpGet("/admin/export")]
    public async Task<IActionResult> ExportAll(string format = "csv", CancellationToken ct = default)
    {
        format = (format ?? "csv").Trim().ToLowerInvariant();
        if (format != "csv" && format != "json") format = "csv";

        await _audit.LogAsync(new AuditEntry {
            Action = "Export.Run",
            DetailsJson = $"{{\"format\":\"{format}\"}}"
        }, ct);

        var fileName = $"becs_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        await using (var con = new SqliteConnection(_connStr))
        {
            await con.OpenAsync(ct);

            // collect tables (exclude sqlite internal)
            var tables = await GetUserTablesAsync(con, ct);

            // ensure audit_logs included
            if (!tables.Contains("audit_logs", StringComparer.OrdinalIgnoreCase))
                tables.Add("audit_logs");

            foreach (var table in tables)
            {
                var entry = zip.CreateEntry($"{table}.{format}", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();

                switch (format)
                {
                    case "json":
                        await WriteJsonAsync(con, table, entryStream, ct);
                        break;
                    default:
                        await WriteCsvAsync(con, table, entryStream, ct);
                        break;
                }
            }
        }

        ms.Position = 0;
        return File(ms, "application/zip", fileName);
    }

    private static async Task<List<string>> GetUserTablesAsync(SqliteConnection con, CancellationToken ct)
    {
        var list = new List<string>();
        await using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT name FROM sqlite_master 
                            WHERE type='table' AND name NOT LIKE 'sqlite_%'
                            ORDER BY name;";
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
            list.Add(rdr.GetString(0));
        return list;
    }

    private static async Task WriteCsvAsync(SqliteConnection con, string table, Stream s, CancellationToken ct)
    {
        await using var writer = new StreamWriter(s, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

        await using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{table}\";";
        await using var rdr = await cmd.ExecuteReaderAsync(ct);

        // header
        var colCount = rdr.FieldCount;
        var headers = new string[colCount];
        for (int i = 0; i < colCount; i++) headers[i] = EscapeCsv(rdr.GetName(i));
        await writer.WriteLineAsync(string.Join(",", headers));

        // rows
        while (await rdr.ReadAsync(ct))
        {
            var vals = new string[colCount];
            for (int i = 0; i < colCount; i++)
            {
                var v = rdr.IsDBNull(i) ? "" : rdr.GetValue(i)?.ToString() ?? "";
                vals[i] = EscapeCsv(v);
            }
            await writer.WriteLineAsync(string.Join(",", vals));
        }
        await writer.FlushAsync();
    }

    private static async Task WriteJsonAsync(SqliteConnection con, string table, Stream s, CancellationToken ct)
    {
        await using var cmd = con.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{table}\";";
        await using var rdr = await cmd.ExecuteReaderAsync(ct);

        await using var w = new Utf8JsonWriter(s, new JsonWriterOptions { Indented = true });

        w.WriteStartArray();
        var colCount = rdr.FieldCount;
        var names = new string[colCount];
        for (int i = 0; i < colCount; i++) names[i] = rdr.GetName(i);

        while (await rdr.ReadAsync(ct))
        {
            w.WriteStartObject();
            for (int i = 0; i < colCount; i++)
            {
                w.WritePropertyName(names[i]);
                if (rdr.IsDBNull(i)) { w.WriteNullValue(); }
                else
                {
                    var val = rdr.GetValue(i);
                    switch (val)
                    {
                        case long l:    w.WriteNumberValue(l); break;
                        case int ii:    w.WriteNumberValue(ii); break;
                        case double d:  w.WriteNumberValue(d); break;
                        case float f:   w.WriteNumberValue(f); break;
                        case bool b:    w.WriteBooleanValue(b); break;
                        default:        w.WriteStringValue(val.ToString()); break;
                    }
                }
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        await w.FlushAsync(ct);
    }

    private static string EscapeCsv(string input)
    {
        if (input.IndexOfAny(new [] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + input.Replace("\"", "\"\"") + "\"";
        return input;
    }
}
