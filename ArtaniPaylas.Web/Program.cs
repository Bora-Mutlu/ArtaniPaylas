using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Data;
using ArtaniPaylas.Data.Services;
using ArtaniPaylas.Web.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
var authCookieName = isDevelopment ? "ArtaniPaylas.Auth" : "__Host-ArtaniPaylas.Auth";
var antiCsrfCookieName = isDevelopment ? "ArtaniPaylas.AntiCsrf" : "__Host-ArtaniPaylas.AntiCsrf";

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var npgsqlConnectionString = new NpgsqlConnectionStringBuilder(connectionString);
if (string.IsNullOrWhiteSpace(npgsqlConnectionString.Password))
{
    var configuredPassword = builder.Configuration["ConnectionStrings:DefaultConnectionPassword"]
        ?? builder.Configuration["Supabase:DbPassword"]
        ?? builder.Configuration["Database:Password"];

    if (!string.IsNullOrWhiteSpace(configuredPassword))
    {
        npgsqlConnectionString.Password = configuredPassword;
    }
}

if (string.IsNullOrWhiteSpace(npgsqlConnectionString.Password))
{
    throw new InvalidOperationException(
        "Database password is missing. Set 'ConnectionStrings:DefaultConnection' with Password=... or provide 'ConnectionStrings:DefaultConnectionPassword'.");
}

var dataSourceBuilder = new NpgsqlDataSourceBuilder(npgsqlConnectionString.ConnectionString);

// Supabase does not require mutual TLS. Clearing the client certificate collection
// avoids Windows Schannel trying to acquire a client identity during SSL negotiation.
dataSourceBuilder.UseClientCertificatesCallback(static certificates =>
{
    certificates.Clear();
});

var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(dataSource));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 6;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.IterationCount = 120_000;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = authCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    // MVC URL paths instead of Identity Razor Pages
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddScoped<IListingStatusService, ListingStatusService>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    if (!await roleManager.RoleExistsAsync("User"))
    {
        await roleManager.CreateAsync(new IdentityRole("User"));
    }

    var adminEmail = "admin@artanipaylas.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "Sistem Yöneticisi",
            IsActive = true
        };
        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
