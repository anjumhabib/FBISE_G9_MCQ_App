namespace McqExam.Domain;

public sealed class RefreshSession
{
    public Guid Id { get; init; }
    public int UserId { get; init; }
    public User User { get; init; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; init; }
}
