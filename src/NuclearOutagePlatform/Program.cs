using Microsoft.EntityFrameworkCore;
using MVC_EF_Start_8.DataAccess;
using MVC_EF_Start_8.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---------------------------------------------------------
// Postgres instead of Azure SQL (see README's "Why Postgres" section).
// Connection string comes from configuration -- appsettings.json holds
// only the non-secret shape; the actual host/password come from the
// ConnectionStrings__DefaultConnection environment variable (see
// docker-compose.yml and .env.example), never committed to source.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// OutageService depends on ApplicationDbContext (Scoped by default from
// AddDbContext), so it must also be Scoped -- NOT Singleton. The original
// registered NuclearOutageService as a Singleton, which only "worked"
// because it never actually held a DbContext (it held an in-memory List
// instead). Registering a Scoped-dependent service as Singleton is a
// classic ASP.NET Core bug: EF Core's DbContext isn't thread-safe, and a
// true Singleton is shared across every concurrent request.
builder.Services.AddScoped<OutageService>();

// --- EIA API client -----------------------------------------------------
builder.Services.AddHttpClient("EIA_API", client =>
{
    client.BaseAddress = new Uri("https://api.eia.gov/v2/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- MVC ---------------------------------------------------------------
builder.Services.AddControllersWithViews();

var app = builder.Build();

// --- Apply migrations automatically on startup --------------------------
// Fine for a portfolio project / small deployment; a larger real system
// would run migrations as a separate release step instead of on every
// app boot.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
