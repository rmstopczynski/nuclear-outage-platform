using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MVC_EF_Start_8.Controllers;
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

// EiaIngestionService is Scoped (it depends on OutageService, which is
// Scoped). EiaIngestionBackgroundService is a Singleton (all
// BackgroundServices are) and creates its own DI scope per run rather
// than holding a long-lived EiaIngestionService instance -- see that
// class for the lifetime-mismatch reasoning.
builder.Services.AddScoped<EiaIngestionService>();
builder.Services.AddHostedService<EiaIngestionBackgroundService>();

// --- Auth (Step 3) -------------------------------------------------------
// AuthService and WatchlistService both depend on ApplicationDbContext, so
// both are Scoped -- same reasoning as OutageService above.
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WatchlistService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NuclearOutagePlatform";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NuclearOutagePlatform";

// JWT stored in an HttpOnly cookie rather than a bearer header -- this is a
// server-rendered MVC app with full-page navigations, not a JS client that
// can attach an Authorization header. OnMessageReceived pulls the token
// out of the cookie instead of expecting the standard header. The same
// scheme (and the same login/register endpoints) would also accept a real
// "Authorization: Bearer" header with zero changes, which is what lets
// Step 6's React frontend reuse this without a rewrite.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthController.AuthCookieName, out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

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

// UseAuthentication() was missing entirely before Step 3 -- only
// UseAuthorization() existed. Without it, [Authorize] attributes have
// nothing populating User.Identity/claims from the incoming request, so
// they'd either reject everyone or (depending on defaults) do nothing
// useful at all. Must come before UseAuthorization() in the pipeline.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
