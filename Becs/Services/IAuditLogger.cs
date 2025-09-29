using System.Threading;
using System.Threading.Tasks;

public interface IAuditLogger
{
    Task<long> LogAsync(AuditEntry entry, CancellationToken ct = default);
}

public sealed class AuditEntry
{
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string ActorType { get; set; } = "User";
    public string Action { get; set; } = default!;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public bool Success { get; set; } = true;
}