using Microsoft.Data.Sqlite;

public interface IResearcherData
{
    Task<List<ResearchRow>> GetRowsAsync();
}

public sealed class ResearchRow
{
    public string BloodType { get; set; } = "";
    public string Rh { get; set; } = "";
    public string DonationDate { get; set; } = "";
    public string Status { get; set; } = "";
    public string DonationSource { get; set; } = "";
}

public class ResearcherData : IResearcherData
{
    private readonly string _cs;
    public ResearcherData(string cs) => _cs = cs;

    public async Task<List<ResearchRow>> GetRowsAsync()
    {
        var list = new List<ResearchRow>();
        using var con = new SqliteConnection(_cs);
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT blood_type, rh, donation_date, status, donation_source
                            FROM ResearcherView
                            ORDER BY donation_date DESC";
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ResearchRow
            {
                BloodType = r.GetString(0),
                Rh = r.GetString(1),
                DonationDate = r.GetString(2),
                Status = r.GetString(3),
                DonationSource = r.GetString(4)
            });
        }
        return list;
    }
}