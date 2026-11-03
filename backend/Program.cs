using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Services;


var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
}

// --- Banco de Dados ---
var connectionString = DatabaseConfiguration.GetConnectionString(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576;
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    allowedOrigins = ["http://localhost:4200", "https://localhost:4200"];
}

if (!builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins deve conter as origens autorizadas fora do ambiente de desenvolvimento.");
}

// --- CORS (permite requisições do frontend local) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- JWT Auth ---
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "Jwt:Secret não foi configurado. Configure via appsettings.Development.json, user-secrets ou variável Jwt__Secret.");
}

if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("Jwt:Secret deve ter ao menos 32 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("Jwt:Issuer e Jwt:Audience devem ser configurados.");
}

_ = TokenService.GetAccessTokenLifetime(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o => ReverseProxyConfiguration.Configure(o, builder.Configuration));
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (path.Equals("/api/Auth/cadastrar", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/Auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/Auth/refresh", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/Auth/revoke", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetSlidingWindowLimiter(
                $"auth:{clientKey}",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        if (httpContext.Request.Method == "POST" &&
            path.Equals("/api/Orders", StringComparison.OrdinalIgnoreCase))
        {
            var userKey = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? clientKey;
            return RateLimitPartition.GetSlidingWindowLimiter(
                $"checkout:{userKey}",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        if (httpContext.Request.Method == "POST" &&
            path.Equals("/api/payments/webhook", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"webhook:{clientKey}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        return RateLimitPartition.GetNoLimiter("default");
    });
});

// --- Controllers ---
builder.Services.AddControllers();

// --- Swagger/OpenAPI ---
builder.Services.AddOpenApi();

// --- Serviços ---
builder.Services.AddScoped<CourseService>();
builder.Services.AddSingleton<CourseRecommendationEngine>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PromoCodeService>();
builder.Services.AddScoped<EnrollmentService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddHostedService<PendingOrderExpirationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ParticipationService>();


var app = builder.Build();

// --- Desenvolvimento local ou demonstracao explicitamente habilitada ---
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --- Middleware ---
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    if (app.Configuration.GetValue<bool?>("Https:RedirectEnabled") ?? true)
    {
        app.UseHttpsRedirection();
    }
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var ready = await db.Database.CanConnectAsync(cancellationToken);
    return ready ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503);
}).AllowAnonymous();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.Run();
