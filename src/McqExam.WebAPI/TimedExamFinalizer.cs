using McqExam.Application;

namespace McqExam.WebAPI;

internal sealed class TimedExamFinalizer(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<TimedExamFinalizer> logger) : BackgroundService
{
    private const int BatchSize = 32;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim gate = new(1);
    private DateTimeOffset nextCheck;

    public async Task Sweep(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow();
            if (now < nextCheck) return;
            await using var scope = scopes.CreateAsyncScope();
            var finalized = await scope.ServiceProvider.GetRequiredService<ExamService>().FinalizeOverdue(BatchSize, ct);
            nextCheck = finalized < BatchSize ? now.Add(Interval) : now;
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            nextCheck = clock.GetUtcNow().Add(Interval);
            logger.LogWarning(error, "Timed exam finalization will retry on the next sweep.");
        }
        finally
        {
            gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        try
        {
            do { await Sweep(stoppingToken); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
