using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using CarShowJudging.Infrastructure.Services;
using CarShowJudging.Web;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    options.UseSqlite(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

var uploadPath = Path.Combine(builder.Environment.WebRootPath, "uploads", "vehicles");
builder.Services.AddSingleton<IBlobStorageService>(
    new LocalFileStorageService(uploadPath, "/uploads/vehicles"));

builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Trust X-Forwarded-Proto/-For from the reverse proxy (Cloudflare Tunnel or Caddy) so the app
// correctly reports Request.IsHttps/Scheme and logs real client IPs, since Kestrel only sees
// the plain-HTTP hop from the proxy otherwise.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// The proxy is always cloudflared/Caddy on the internal Docker network, whose IP isn't
// deterministic, so trust forwarded headers regardless of the immediate peer address.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// The classic AddServerSideBlazor()/MapBlazorHub() hosting model (used until this migration)
// has a broken _framework/blazor.server.js endpoint in .NET 10 — the script 404s, which
// silently breaks all interactive Blazor components (buttons appear to do nothing, since the
// client-side circuit script never loads). The unified AddRazorComponents()/MapRazorComponents()
// model below is the only reliably-working option in .NET 10. See:
// https://github.com/dotnet/aspnetcore/issues/66175
app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await SeedAsync(app);

app.Run();

static async Task SeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var role in new[] { "Admin", "Judge", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    if (await userManager.FindByNameAsync("admin") is null)
    {
        var adminPassword = app.Configuration["Seed:AdminPassword"] ?? "password123";
        if (!app.Environment.IsDevelopment() && adminPassword == "password123")
        {
            throw new InvalidOperationException(
                "Set Seed:AdminPassword (e.g. via the SEED__ADMINPASSWORD environment variable) " +
                "to a strong password before running outside Development.");
        }

        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@carshow.local",
            DisplayName = "Administrator",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}
