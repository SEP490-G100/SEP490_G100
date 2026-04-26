using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Hubs;
using Nanny_BackEnd.Repositories.Interfaces;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Services.Interfaces;
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

builder.Services.AddControllers(options =>
    {
        options.ModelMetadataDetailsProviders.Add(new VietnameseValidationMetadataProvider());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });
builder.Services.AddMemoryCache();

// SignalR (built-in, không cần package)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

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
    ?? ["http://localhost:5200", "https://localhost:7183", "http://localhost:5001", "https://localhost:5001"];

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
builder.Services.AddHttpClient("Casso", client =>
{
    client.BaseAddress = new Uri("https://oauth.casso.vn/v2/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("PayOs", client =>
{
    client.BaseAddress = new Uri("https://api-merchant.payos.vn/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.Configure<CassoOptions>(builder.Configuration.GetSection("Casso"));
builder.Services.Configure<PayOsOptions>(builder.Configuration.GetSection("PayOs"));
// Nominatim (OpenStreetMap geocoding) — User-Agent bắt buộc theo ToS
builder.Services.AddHttpClient("Nominatim", c =>
{
    c.BaseAddress = new Uri("https://nominatim.openstreetmap.org");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("NannyMatchApp/1.0 (contact@nannymatch.vn)");
    c.Timeout = TimeSpan.FromSeconds(5);
});

// DI — Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
builder.Services.AddScoped<IParentRepository, ParentRepository>();
builder.Services.AddScoped<IContactRequestRepository, ContactRequestRepository>();
builder.Services.AddScoped<IChildRepository, ChildRepository>();
// Search feature (SD1B)
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IVerificationRequestRepository, VerificationRequestRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IUserSubscriptionRepository, UserSubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IContractRepository, ContractRepository>();
builder.Services.AddScoped<IHiringRepository, HiringRepository>();
builder.Services.AddScoped<ICommunicationRepository, CommunicationRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IExportRepository, ExportRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IFaqRepository, FaqRepository>();
builder.Services.AddScoped<IBlogCategoryRepository, BlogCategoryRepository>();
builder.Services.AddScoped<IBlogRepository, BlogRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<INannyProfileRepository, NannyProfileRepository>();
builder.Services.AddScoped<INannySkillRepository, NannySkillRepository>();
builder.Services.AddScoped<INannyCertificateRepository, NannyCertificateRepository>();
builder.Services.AddScoped<INannyAvailabilityRepository, NannyAvailabilityRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<IRecommendationConfigRepository, RecommendationConfigRepository>();

// DI — Services
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<INannyService, NannyService>();
builder.Services.AddSingleton<PasswordValidator>();
// Search feature (SD1B)
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IGeocodingService, GeocodingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVerificationRequestService, VerificationRequestService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IContactRequestService, ContactRequestService>();
builder.Services.AddScoped<ICassoService, CassoService>();
builder.Services.AddScoped<IPayOsService, PayOsService>();
builder.Services.AddScoped<ICommunicationService, CommunicationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IHiringService, HiringService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<IBlogCategoryService, BlogCategoryService>();
builder.Services.AddScoped<IBlogService, BlogService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// Recommendation feature
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<ISearchService, SearchService>();

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
app.UseStaticFiles();
app.UseRouting();
app.UseCors("RestApi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// SignalR hub endpoint — dùng SignalR CORS policy
app.MapHub<ChatHub>("/hubs/chat").RequireCors("SignalR");

app.Run();
