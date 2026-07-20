using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MVC_EF_Start_8.DataAccess
{
    /// <summary>
    /// Lets `dotnet ef migrations add` construct ApplicationDbContext
    /// without needing Program.cs's full configuration pipeline (env
    /// vars, docker-compose, etc.) to be wired up. The connection string
    /// here is never actually used to connect to anything -- generating
    /// a migration only needs to know the schema shape and the Postgres
    /// provider, not a live, reachable database.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=design_time_only;Username=postgres;Password=postgres");
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
