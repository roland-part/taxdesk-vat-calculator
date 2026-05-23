using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using VatCalculator.Api.Services;

QuestPDF.Settings.License = LicenseType.Community;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(services)
       .WriteTo.Console()
       .WriteTo.File(
           "logs/app-.log",
           rollingInterval: RollingInterval.Day,
           retainedFileCountLimit: 7,
           outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");

    var aiConnectionString = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(aiConnectionString))
        cfg.WriteTo.ApplicationInsights(aiConnectionString, new TraceTelemetryConverter());
});

// ── MVC / JSON ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddOpenApi();

// ── CORS — origins from config (AllowedOrigins in appsettings) ────────────────
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Upload: 10 requests per minute per IP
    options.AddPolicy("upload", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit            = 10,
                Window                 = TimeSpan.FromMinutes(1),
                QueueProcessingOrder   = QueueProcessingOrder.OldestFirst,
                QueueLimit             = 0,
            }));

    // PDF generation: 30 requests per minute per IP
    options.AddPolicy("pdf", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit            = 30,
                Window                 = TimeSpan.FromMinutes(1),
                QueueProcessingOrder   = QueueProcessingOrder.OldestFirst,
                QueueLimit             = 0,
            }));
});

// ── Request timeouts ──────────────────────────────────────────────────────────
builder.Services.AddRequestTimeouts(options =>
{
    options.AddPolicy("upload", TimeSpan.FromSeconds(30));
    options.AddPolicy("pdf",    TimeSpan.FromSeconds(60));
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<ICsvParserService, CsvParserService>();
builder.Services.AddScoped<IVatCalculationService, VatCalculationService>();
builder.Services.AddScoped<IVatReportPdfService, VatReportPdfService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ── Security headers (applied to every response) ──────────────────────────────
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h.Append("X-Content-Type-Options",  "nosniff");
    h.Append("X-Frame-Options",         "DENY");
    h.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    h.Append("Permissions-Policy",      "camera=(), microphone=(), geolocation=()");
    // Pure JSON API — no resources should be fetched from these responses
    h.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
    await next();
});

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseRequestTimeouts();
app.UseAuthorization();
app.MapControllers();

app.Run();
