using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using WebSite.Infrastructure;
using WebSite.Models.Storage;
using WebSite.Services;
using WebSite.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.ModelMetadataDetailsProviders.Add(new VietnameseValidationMetadataProvider());
});
builder.Services.AddRazorPages();
builder.Services.Configure<AzureBlobStorageOptions>(
    builder.Configuration.GetSection(AzureBlobStorageOptions.SectionName));
builder.Services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();
builder.Services.AddSignalR();

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

// HttpClient → Backend API
builder.Services.AddHttpClient("BackendApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.MapRazorPages();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var hasAnyRole = context.User.Claims.Any(claim =>
            claim.Type == ClaimTypes.Role &&
            !string.IsNullOrWhiteSpace(claim.Value));

        if (!hasAnyRole)
        {
            var path = context.Request.Path;
            var isChooseRolePath = path.StartsWithSegments("/Auth/ChooseRole", StringComparison.OrdinalIgnoreCase);
            var isAuthLoginPath = path.StartsWithSegments("/Auth/Login", StringComparison.OrdinalIgnoreCase);
            var isAuthLogoutPath = path.StartsWithSegments("/Auth/Logout", StringComparison.OrdinalIgnoreCase);
            var isGet = HttpMethods.IsGet(context.Request.Method);
            var secFetchDest = context.Request.Headers["Sec-Fetch-Dest"].ToString();
            var accept = context.Request.Headers.Accept.ToString();
            var isTopLevelNavigation =
                isGet &&
                (
                    string.Equals(secFetchDest, "document", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(accept) &&
                     accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                );

            if (isTopLevelNavigation && !isChooseRolePath && !isAuthLoginPath && !isAuthLogoutPath)
            {
                context.Session.Clear();
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Auth/Login");
                return;
            }
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();
