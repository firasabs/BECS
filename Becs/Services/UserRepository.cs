using Microsoft.Data.Sqlite;

public interface IUserRepository
{
    Task<UserRecord?> FindByUsernameAsync(string username);
    Task<int> CreateAsync(string username, string passwordHash, string role);
    Task SetLastLoginAsync(long id, DateTime ts);
    Task<bool> UsernameExistsAsync(string username);
}

public class UserRepository : IUserRepository
{
    private readonly string _cs;
    public UserRepository(string cs) => _cs = cs;

    public async Task<UserRecord?> FindByUsernameAsync(string username)
    {
        using var con = new SqliteConnection(_cs);
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT id, username, password_hash, role, last_login
                        FROM Users WHERE username=@u LIMIT 1";
        cmd.Parameters.AddWithValue("@u", username);
        using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        DateTime? lastLogin = null;
        if (!r.IsDBNull(4))
        {
            var val = r.GetValue(4)?.ToString();
            if (!string.IsNullOrWhiteSpace(val) && DateTime.TryParse(val, out var parsed))
                lastLogin = parsed;
        }

        return new UserRecord
        {
            Id = r.GetInt64(0),
            Username = r.GetString(1),
            PasswordHash = r.GetString(2),
            Role = r.GetString(3),
            LastLogin = lastLogin
        };
    }
    
    public async Task<int> CreateAsync(string username, string passwordHash, string role)
    {
        using var con = new SqliteConnection(_cs);
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO Users(username,password_hash,role)
                            VALUES(@u,@p,@r)";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", passwordHash);
        cmd.Parameters.AddWithValue("@r", role);
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetLastLoginAsync(long id, DateTime ts)
    {
        using var con = new SqliteConnection(_cs);
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE Users SET last_login=@ts WHERE id=@id";
        cmd.Parameters.AddWithValue("@ts", ts);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        using var con = new SqliteConnection(_cs);
        await con.OpenAsync();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Users WHERE username=@u LIMIT 1";
        cmd.Parameters.AddWithValue("@u", username);
        var res = await cmd.ExecuteScalarAsync();
        return res != null;
    }
}
