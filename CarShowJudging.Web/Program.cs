using Azure.Storage.Blobs;
using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using CarShowJudging.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    if (builder.Environment.IsDevelopment())
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
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

var blobConnectionString = builder.Configuration["AzureBlobStorage:ConnectionString"];
if (blobConnectionString == "UseDevelopmentStorage=true")
{
    var uploadPath = Path.Combine(builder.Environment.WebRootPath, "uploads", "vehicles");
    builder.Services.AddSingleton<IBlobStorageService>(
        new LocalFileStorageService(uploadPath, "/uploads/vehicles"));
}
else
{
    builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));
    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
}

builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INoteService, NoteService>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

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
        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = "admin@carshow.local",
            DisplayName = "Administrator",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, "password123");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}
