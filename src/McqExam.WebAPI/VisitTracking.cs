using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Threading.Channels;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace McqExam.WebAPI;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class TrackVisitAttribute(UserVisitEventType eventType) : Attribute
{
    internal UserVisitEventType EventType { get; } = eventType;
}

internal sealed class VisitTrackingFilter(VisitQueue queue, TimeProvider clock) : IAsyncActionFilter
{
    private const string SessionCookie = "mcq-visit";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tracking = context.ActionDescriptor.EndpointMetadata.OfType<TrackVisitAttribute>().LastOrDefault();
        var outcome = await next();
        if (tracking is null || outcome.Canceled || outcome.Exception is not null ||
            outcome.Result is IStatusCodeActionResult { StatusCode: < 200 or >= 300 })
            return;

        var http = context.HttpContext;
        int? userId;
        if (tracking.EventType == UserVisitEventType.Login)
        {
            if (outcome.Result is not ObjectResult { Value: AuthSession session })
                return;

            userId = session.User.Id;
        }
        else
        {
            if (http.User.IsInRole(nameof(UserRole.Admin)))
                return;

            userId = int.TryParse(http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;
        }

        var sessionKey = http.Request.Cookies[SessionCookie];
        if (sessionKey is not { Length: 32 } || !sessionKey.All(Uri.IsHexDigit))
        {
            sessionKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            http.Response.Cookies.Append(SessionCookie, sessionKey, new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, Path = "/"
            });
        }

        var path = http.Request.Path.Value ?? "/";
        var agent = http.Request.Headers.UserAgent.ToString();
        queue.TryWrite(new UserVisitLog
        {
            UserId = userId, SessionKey = sessionKey, EventType = tracking.EventType,
            Path = path[..Math.Min(path.Length, 512)],
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = agent.Length == 0 ? null : agent[..Math.Min(agent.Length, 512)],
            VisitedAt = clock.GetUtcNow().UtcDateTime
        });
    }
}

internal sealed class VisitQueue
{
    private readonly Channel<UserVisitLog> channel = Channel.CreateBounded<UserVisitLog>(
        new BoundedChannelOptions(2048) { SingleReader = true });

    internal ChannelReader<UserVisitLog> Reader => channel.Reader;
    internal void TryWrite(UserVisitLog visit) => channel.Writer.TryWrite(visit);
    internal void Complete() => channel.Writer.TryComplete();
}

internal sealed class VisitWriter(VisitQueue queue, IServiceScopeFactory scopes, ILogger<VisitWriter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<UserVisitLog>(100);
            while (batch.Count < 100 && queue.Reader.TryRead(out var visit))
                batch.Add(visit);

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IUserVisitStore>().Save(batch, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not save a batch of {VisitCount} visit events.", batch.Count);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        try
        {
            if (ExecuteTask is { } task)
                await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }
}
