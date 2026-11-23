using System.Security.Cryptography;
using System.Text.Json;
using McqExam.Application;
using Microsoft.AspNetCore.DataProtection;

namespace McqExam.WebAPI;

public sealed class SampleQuizTickets(IDataProtectionProvider protection) : ISampleQuizTickets
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("PublicSampleQuiz.v1")
        .ToTimeLimitedDataProtector();

    public string Issue(int[] questionIds) => protector.Protect(JsonSerializer.Serialize(questionIds), TimeSpan.FromMinutes(30));

    public int[] Read(string ticket)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(protector.Unprotect(ticket)) ?? [];
        }
        catch (Exception error) when (error is CryptographicException or JsonException)
        {
            throw new RequestFailure("This sample quiz is invalid or has expired. Start a new quiz.");
        }
    }
}
