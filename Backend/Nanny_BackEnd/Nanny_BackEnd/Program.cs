using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Nanny_BackEnd");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });

// SignalR (built-in, không cần package)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddEndpointsApiExplorer();

// Swagger + JWT
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// DbContext
builder.Services.AddDbContext<Sep490NannyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("MyCnn"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

// JWT Authentication — hỗ trợ cả Bearer header lẫn query string (SignalR WebSocket)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR gửi token qua query string khi dùng WebSocket
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// CORS
// CORS — frontend origin cần AllowCredentials để SignalR WebSocket hoạt động
var frontendOrigins = builder.Configuration.GetSection("FrontendOrigins").Get<string[]>()
    ?? ["http://localhost:5001", "https://localhost:5001"];

builder.Services.AddCors(options =>
{
    // Policy cho REST API (giữ nguyên)
    options.AddPolicy("RestApi",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    // Policy cho SignalR (cần WithOrigins + AllowCredentials)
    options.AddPolicy("SignalR",
        policy => policy
            .WithOrigins(frontendOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddHttpClient();
builder.Services.Configure<VietQrOptions>(builder.Configuration.GetSection("VietQr"));
builder.Services.AddHttpClient("VietQr", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["VietQr:BaseUrl"] ?? "https://api.vietqr.io/v2/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
// Nominatim (OpenStreetMap geocoding) — User-Agent bắt buộc theo ToS
builder.Services.AddHttpClient("Nominatim", c =>
{
    c.BaseAddress = new Uri("https://nominatim.openstreetmap.org");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("NannyMatchApp/1.0 (contact@nannymatch.vn)");
    c.Timeout = TimeSpan.FromSeconds(5);
});

// DI — Repositories
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RefreshTokenRepository>();
builder.Services.AddScoped<OtpRepository>();
builder.Services.AddScoped<ParentRepository>();
builder.Services.AddScoped<ChildRepository>();
// Search feature (SD1B)
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<FavoriteRepository>();
builder.Services.AddScoped<VerificationRequestRepository>();
builder.Services.AddScoped<TransactionRepository>();
builder.Services.AddScoped<UserSubscriptionRepository>();
builder.Services.AddScoped<SubscriptionRepository>();
builder.Services.AddScoped<ContractRepository>();
builder.Services.AddScoped<CommunicationRepository>();

// DI — Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<NannyProfileRepository>();
builder.Services.AddScoped<NannySkillRepository>();
builder.Services.AddScoped<NannyAvailabilityRepository>();
builder.Services.AddSingleton<PasswordValidator>();
// Search feature (SD1B)
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<GeocodingService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<VerificationRequestService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<VietQrService>();
builder.Services.AddScoped<CommunicationService>();

// Background Services
if (!builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<OtpCleanupService>();
builder.Services.AddHostedService<SubscriptionReminderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("RestApi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SignalR hub endpoint — dùng SignalR CORS policy
app.MapHub<ChatHub>("/hubs/chat").RequireCors("SignalR");

app.Run();
