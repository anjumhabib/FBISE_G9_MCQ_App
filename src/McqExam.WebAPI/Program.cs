using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using McqExam.Application;
using McqExam.Domain;
using McqExam.Infrastructure;
using McqExam.WebAPI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var settingsIndex = builder.Configuration.Sources.ToList().FindLastIndex(source =>
    source is JsonConfigurationSource json &&
    (json.Path == "appsettings.json" || json.Path == $"appsettings.{builder.Environment.EnvironmentName}.json"));
builder.Configuration.Sources.Insert(settingsIndex + 1, new JsonConfigurationSource
{
    Path = "appsettings.Local.json",
    Optional = true,
    FileProvider = builder.Environment.ContentRootFileProvider
});
var jwtKey = builder.Configuration["Jwt:Key"];
if (jwtKey is null && builder.Environment.IsDevelopment())
    jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32 ||
    jwtKey.Trim().Equals("PASTE_YOUR_EXISTING_LIVE_JWT_KEY_HERE", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Configure Jwt__Key with at least 32 bytes.");

var jwt = new JwtOptions(builder.Configuration["Jwt:Issuer"]!, builder.Configuration["Jwt:Audience"]!,
    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)));
builder.Services.AddSingleton(jwt);
if (builder.Environment.IsDevelopment())
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ExamDb>(options =>
    DatabaseSetup.Configure(options, builder.Environment.ContentRootPath, builder.Configuration));
builder.Services.AddScoped<IUserStore, UserStore>();
builder.Services.AddScoped<IExamStore, ExamStore>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<ISessionIssuer, SessionIssuer>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RefreshSessions>();
builder.Services.AddScoped<IRefreshSessionStore, RefreshSessionStore>();
builder.Services.AddScoped<ExamService>();
builder.Services.AddScoped<IPublicStudyStore, PublicStudyStore>();
builder.Services.AddScoped<PublicStudyService>();
builder.Services.AddSingleton<ISampleQuizTickets, SampleQuizTickets>();
builder.Services.AddSingleton<PublicSiteUrls>();
builder.Services.AddSingleton<ClientScripts>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAdConfigService, AdConfigService>();
builder.Services.AddScoped<AdPageRendering>();
builder.Services.AddScoped<IAdminAdStore, AdminAdStore>();
builder.Services.AddScoped<AdminAdService>();
builder.Services.AddScoped<IAdminReportStore, AdminReportStore>();
builder.Services.AddScoped<AdminReportService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddScoped<AdminTrafficService>();
builder.Services.AddSingleton<IDiagramFiles, DiagramFiles>();
builder.Services.AddSingleton(new ReportTimeZones(builder.Configuration["DisplayTimeZone"]));
builder.Services.AddScoped<AdminReportTimeZoneFilter>();
builder.Services.AddSingleton<CsvDownload>();
builder.Services.AddScoped<IUserVisitStore, UserVisitStore>();
builder.Services.AddSingleton<VisitQueue>();
builder.Services.AddHostedService<VisitWriter>();
builder.Services.AddSingleton<TimedExamFinalizer>();
builder.Services.AddHostedService(services => services.GetRequiredService<TimedExamFinalizer>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, IssuerSigningKey = jwt.Key,
        ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromSeconds(15), RoleClaimType = ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuthentication")
                .LogInformation("API bearer authentication failed ({FailureType}). Trace ID {TraceId}.",
                    context.Exception.GetType().Name, context.HttpContext.TraceIdentifier);
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            if (!int.TryParse(context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) || id < 1)
            {
                context.Fail("Invalid account ID.");
                return;
            }
            var services = context.HttpContext.RequestServices;
            var sid = context.Principal!.FindFirst("sid")?.Value;
            if (sid is not null && (!Guid.TryParse(sid, out var sessionId) ||
                !await services.GetRequiredService<IRefreshSessionStore>().IsActive(
                    sessionId, id, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
                    context.HttpContext.RequestAborted)))
            {
                services.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuthentication").LogInformation(
                    "Refresh session is no longer active. Trace ID {TraceId}.", context.HttpContext.TraceIdentifier);
                context.Fail("Session is no longer active.");
                return;
            }
            if (context.Principal.IsInRole(nameof(UserRole.Admin)) &&
                !await context.HttpContext.RequestServices.GetRequiredService<IUserStore>()
                    .IsActiveAdmin(id, context.HttpContext.RequestAborted))
            {
                services.GetRequiredService<ILoggerFactory>().CreateLogger("JwtAuthentication").LogInformation(
                    "Administrator authorization changed. Trace ID {TraceId}.", context.HttpContext.TraceIdentifier);
                context.Fail("The administrator account is inactive or its role has changed.");
            }
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(options => options.Filters.Add<VisitTrackingFilter>()).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UtcInstantJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
});
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("sample-quiz", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("contact", context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FBISE MCQ Exam API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await DatabaseSetup.Initialize(scope.ServiceProvider.GetRequiredService<ExamDb>());
    await scope.ServiceProvider.GetRequiredService<AdminSeeder>()
        .Seed(app.Configuration["Admin:Email"], app.Configuration["Admin:Password"]);
}

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is RequestFailure failure)
        await Results.Problem(statusCode: failure.Status, detail: failure.Message).ExecuteAsync(context);
    else
        await Results.Problem(statusCode: 500).ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
var scripts = app.Services.GetRequiredService<ClientScripts>();
var scriptCache = app.Environment.IsDevelopment()
    ? "no-cache"
    : "public, max-age=31536000, immutable";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = scripts.Files,
    RequestPath = scripts.RequestPath,
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = scriptCache
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/js"))
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && HttpMethods.IsGet(context.Request.Method) &&
        (context.Request.Path.StartsWithSegments("/api/admin") || context.Request.Path.StartsWithSegments("/api/reports")))
        await context.RequestServices.GetRequiredService<TimedExamFinalizer>().Sweep(context.RequestAborted);
    await next();
});
app.MapControllers();
app.Map("/api/{**unknown}", () => Results.Problem(statusCode: 404));
app.MapFallback(() => Results.NotFound());
app.Run();
