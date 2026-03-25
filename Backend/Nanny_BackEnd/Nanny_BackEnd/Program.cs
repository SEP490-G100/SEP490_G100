using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
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

// JWT Authentication
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
    });

// CORS
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddPolicy("UiPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
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
builder.Services.AddScoped<FaqRepository>();
builder.Services.AddScoped<BlogCategoryRepository>();
builder.Services.AddScoped<BlogRepository>();

// DI — Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<NannyService>();
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
builder.Services.AddScoped<FaqService>();
builder.Services.AddScoped<BlogCategoryService>();
builder.Services.AddScoped<BlogService>();

// Recommendation feature
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddScoped<RecommendationRepository>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<RecommendationService>();


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

app.UseHttpsRedirection();

app.UseCors("UiPolicy");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
