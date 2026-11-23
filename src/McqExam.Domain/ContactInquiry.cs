namespace McqExam.Domain;

public sealed class ContactInquiry
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}
