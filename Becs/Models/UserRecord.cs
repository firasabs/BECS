public sealed class UserRecord
{
    public long   Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = ""; // admin|user|researcher
    public DateTime? LastLogin { get; set; }
}