// Data/SqliteIssueRepository.cs
using Microsoft.Data.Sqlite;
using Becs.Models;

namespace Becs.Data
{
    public interface IIssueRepository
    {
        Task<(List<BloodUnitVm> chosen, List<AltSuggestion> suggestions)>
            SelectForRoutineAsync(string abo, string rh, int qty, CancellationToken ct = default);

        Task<List<BloodUnitVm>> IssueByIdsAsync(List<string> ids, string issueType, CancellationToken ct = default);

        Task<int> CountONegAsync(CancellationToken ct = default);
        Task<List<BloodUnitVm>> IssueEmergencyONegAsync(CancellationToken ct = default);
    }

    public class SqliteIssueRepository : IIssueRepository
    {
        private readonly string _cs;
        public SqliteIssueRepository(string connectionString) => _cs = connectionString;
        private static string AboCompatSql => @"
(
 (@rec_abo='O'  AND bu.ABO='O') OR
 (@rec_abo='A'  AND bu.ABO IN ('O','A')) OR
 (@rec_abo='B'  AND bu.ABO IN ('O','B')) OR
 (@rec_abo='AB' AND bu.ABO IN ('O','A','B','AB'))
)";

        private static string RhCompatSql => @"
(
 (@rec_rh='-' AND bu.Rh='-')
 OR
 (@rec_rh='+' AND bu.Rh IN ('+','-'))
)";

        private static string FreqOrderSql => @"
CASE bu.ABO || bu.Rh
  WHEN 'O+'  THEN 0.37 WHEN 'A+'  THEN 0.34
  WHEN 'B+'  THEN 0.10 WHEN 'AB+' THEN 0.04
  WHEN 'O-'  THEN 0.06 WHEN 'A-'  THEN 0.06
  WHEN 'B-'  THEN 0.02 WHEN 'AB-' THEN 0.01
  ELSE 0 END";

        public async Task<(List<BloodUnitVm> chosen, List<AltSuggestion> suggestions)>
            SelectForRoutineAsync(string abo, string rh, int qty, CancellationToken ct = default)
        {
            var chosen = new List<BloodUnitVm>();
            var suggestions = new List<AltSuggestion>();

            var sql = $@"
SELECT bu.Id, bu.ABO, bu.Rh, bu.DonationDate, bu.Status,
       d.DonorId, d.DonorName,
       (CASE WHEN bu.ABO=@rec_abo AND bu.Rh=@rec_rh THEN 1 ELSE 0 END) AS exact_match,
       {FreqOrderSql} AS freq
FROM BloodUnits bu
LEFT JOIN Donors d ON d.DonorId = bu.DonorId
WHERE bu.Status='Available' AND {AboCompatSql} AND {RhCompatSql}
ORDER BY exact_match DESC, freq DESC, datetime(bu.DonationDate) ASC
LIMIT 500;";

            await using var conn = new SqliteConnection(_cs);
            await conn.OpenAsync(ct);

            var compatibles = new List<BloodUnitVm>();
            await using (var cmd = new SqliteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@rec_abo", abo);
                cmd.Parameters.AddWithValue("@rec_rh", rh);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    compatibles.Add(new BloodUnitVm
                    {
                        Id = r["Id"].ToString(),
                        ABO = r.GetString(1),
                        Rh = r.GetString(2),
                        DonationDate = DateTime.Parse(r.GetString(3)),
                        Status = r.GetString(4),
                        DonorId = r.IsDBNull(5) ? "" : r.GetString(5),
                        DonorName = r.IsDBNull(6) ? "" : r.GetString(6),
                    });
                }
            }

            if (compatibles.Count == 0)
            {
                suggestions = await SuggestAlternativesAsync(conn, abo, rh, ct);
                return (chosen, suggestions);
            }

            foreach (var u in compatibles)
            {
                if (chosen.Count >= qty) break;
                chosen.Add(u);
            }

            if (chosen.Count < qty)
                suggestions = await SuggestAlternativesAsync(conn, abo, rh, ct);

            return (chosen, suggestions);
        }

        private async Task<List<AltSuggestion>> SuggestAlternativesAsync(SqliteConnection conn, string abo, string rh, CancellationToken ct)
        {
            var sql = $@"
SELECT bu.ABO || bu.Rh AS bt, COUNT(*) AS cnt, {FreqOrderSql} AS freq
FROM BloodUnits bu
WHERE bu.Status='Available' AND {AboCompatSql} AND {RhCompatSql}
  AND NOT (bu.ABO=@rec_abo AND bu.Rh=@rec_rh)
GROUP BY bt
ORDER BY freq DESC, cnt DESC
LIMIT 6;";

            var list = new List<AltSuggestion>();
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@rec_abo", abo);
            cmd.Parameters.AddWithValue("@rec_rh", rh);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new AltSuggestion(r.GetString(0), r.GetInt32(1)));
            return list;
        }

        public async Task<List<BloodUnitVm>> IssueByIdsAsync(List<string> ids, string issueType,
            CancellationToken ct = default)
        {
            var idList = ids.Select(g => g.ToString()).ToList();
            if (idList.Count == 0) return new();

            await using var conn = new SqliteConnection(_cs);
            await conn.OpenAsync(ct);
            await using (var pragma = new SqliteCommand("PRAGMA foreign_keys=ON;", conn))
                await pragma.ExecuteNonQueryAsync(ct);

            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                // 1) Select rows we are going to issue
                var placeholders = string.Join(",", idList.Select((_, i) => $"@p{i}"));
                var selectSql = $@"
SELECT bu.Id, bu.ABO, bu.Rh, bu.DonationDate, bu.Status, d.DonorId, d.DonorName
FROM BloodUnits bu
LEFT JOIN Donors d ON d.DonorId = bu.DonorId
WHERE bu.Status='Available' AND bu.Id IN ({placeholders});";

                var toIssue = new List<BloodUnitVm>();
                await using (var sel = new SqliteCommand(selectSql, conn, tx))
                {
                    for (int i = 0; i < idList.Count; i++)
                        sel.Parameters.AddWithValue($"@p{i}", idList[i]);

                    await using var r = await sel.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        toIssue.Add(new BloodUnitVm
                        {
                            Id = r.GetString(0),
                            ABO = r.GetString(1),
                            Rh = r.GetString(2),
                            DonationDate = DateTime.Parse(r.GetString(3)),
                            Status = r.GetString(4),
                            DonorId = r.IsDBNull(5) ? "" : r.GetString(5),
                            DonorName = r.IsDBNull(6) ? "" : r.GetString(6),
                        });
                    }
                }
                if (toIssue.Count == 0) { await tx.RollbackAsync(ct); return new(); }

                // 2) Update status
                var updSql = $@"UPDATE BloodUnits SET Status='Issued' WHERE Id IN ({placeholders});";
                await using (var upd = new SqliteCommand(updSql, conn, tx))
                {
                    for (int i = 0; i < idList.Count; i++)
                        upd.Parameters.AddWithValue($"@p{i}", idList[i]);
                    await upd.ExecuteNonQueryAsync(ct);
                }

                // 3) Insert Issues
                const string insSql = @"INSERT INTO Issues (IssueId, BloodUnitId, ABO, Rh, IssueDate, IssueType)
                                        VALUES (@iid, @bid, @abo, @rh, @ts, @type);";
                foreach (var u in toIssue)
                {
                    await using var ins = new SqliteCommand(insSql, conn, tx);
                    ins.Parameters.AddWithValue("@iid", Guid.NewGuid().ToString());
                    ins.Parameters.AddWithValue("@bid", u.Id.ToString());
                    ins.Parameters.AddWithValue("@abo", u.ABO);
                    ins.Parameters.AddWithValue("@rh", u.Rh);
                    ins.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));
                    ins.Parameters.AddWithValue("@type", issueType);
                    await ins.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                return toIssue;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<int> CountONegAsync(CancellationToken ct = default)
        {
            const string sql = @"SELECT COUNT(*) FROM BloodUnits WHERE Status='Available' AND ABO='O' AND Rh='-';";
            await using var conn = new SqliteConnection(_cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqliteCommand(sql, conn);
            var n = (long)(await cmd.ExecuteScalarAsync(ct))!;
            return (int)n;
        }

        public async Task<List<BloodUnitVm>> IssueEmergencyONegAsync(CancellationToken ct = default)
        {
            const string sqlIds = @"SELECT Id FROM BloodUnits WHERE Status='Available' AND ABO='O' AND Rh='-';";
            List<string> ids = new List<string>();
            await using var conn = new SqliteConnection(_cs);
            await conn.OpenAsync(ct);
            await using (var cmd = new SqliteCommand(sqlIds, conn))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
            {
                while (await r.ReadAsync(ct))
                    ids.Add(r.GetString(0));
            }
            return await IssueByIdsAsync(ids, "Emergency", ct);
        }
    }
}
