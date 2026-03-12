using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Repositories;
using Nanny_BackEnd.Services;
using Nanny_BackEnd.Validations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyCnn")));

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
// Search feature (SD1B)
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<FavoriteRepository>();

// DI — Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<PasswordValidator>();
// Search feature (SD1B)
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<GeocodingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
