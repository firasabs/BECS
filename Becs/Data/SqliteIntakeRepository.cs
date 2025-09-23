// Data/SqliteIntakeRepository.cs
using Microsoft.Data.Sqlite;
using Becs.Models;

namespace Becs.Data
{
    public interface IIntakeRepository
    {
        Task<IEnumerable<BloodUnitVm>> GetUnitsAsync(CancellationToken ct = default);
        Task InsertDonationAsync(DonationInput input, CancellationToken ct = default);
    }

    public class SqliteIntakeRepository : IIntakeRepository
    {
        private readonly string _cs;
        public SqliteIntakeRepository(IConfiguration cfg)
        {
            _cs = Environment.GetEnvironmentVariable("APP_DB_CS")
               ?? cfg.GetConnectionString("Sqlite")
               ?? "Data Source=becs.db";
        }

        private SqliteConnection Conn()
        {
            var c = new SqliteConnection(_cs);
            return c;
        }

        public async Task<IEnumerable<BloodUnitVm>> GetUnitsAsync(CancellationToken ct = default)
        {
            const string sql = @"
SELECT bu.Id, bu.ABO, bu.Rh, bu.DonationDate, bu.Status, d.DonorId, d.DonorName
FROM BloodUnits bu
LEFT JOIN Donors d ON d.DonorId = bu.DonorId
ORDER BY datetime(bu.DonationDate) DESC, bu.Id DESC
LIMIT 500;";

            var list = new List<BloodUnitVm>();
            await using var conn = Conn();
            await conn.OpenAsync(ct);
            await using var cmd = new SqliteCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new BloodUnitVm
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
            return list;
        }

        public async Task InsertDonationAsync(DonationInput input, CancellationToken ct = default)
        {
            var unitId = Guid.NewGuid().ToString();

            const string upsertDonor = @"
INSERT INTO Donors (DonorId, DonorName)
VALUES (@donorId, @donorName)
ON CONFLICT(DonorId) DO UPDATE SET DonorName = excluded.DonorName;";

            const string insertUnit = @"
INSERT INTO BloodUnits (Id, ABO, Rh, DonationDate, DonorId, Status, DonationSource)
VALUES (@id, @abo, @rh, @date, @donorId, 'Available', 'Soroka');";

            await using var conn = Conn();
            await conn.OpenAsync(ct);

            // Enforce FKs in this connection
            await using (var pragma = new SqliteCommand("PRAGMA foreign_keys=ON;", conn))
                await pragma.ExecuteNonQueryAsync(ct);

            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                await using (var cmd = new SqliteCommand(upsertDonor, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@donorId", input.DonorId);
                    cmd.Parameters.AddWithValue("@donorName", input.DonorName);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var cmd = new SqliteCommand(insertUnit, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", unitId);
                    cmd.Parameters.AddWithValue("@abo", input.ABO);
                    cmd.Parameters.AddWithValue("@rh", input.RhSign);
                    cmd.Parameters.AddWithValue("@date", input.DonationDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@donorId", input.DonorId);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
