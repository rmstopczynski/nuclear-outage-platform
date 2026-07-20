using Microsoft.EntityFrameworkCore;
using MVC_EF_Start_8.Models;

namespace MVC_EF_Start_8.DataAccess
{
    /// <summary>
    /// Replaces the original ApplicationDbContext, which only had
    /// DbSet&lt;Company&gt; and DbSet&lt;Quote&gt; -- leftover scaffold from an
    /// unrelated ASP.NET stock-quote tutorial template, never actually wired
    /// into the app's real request pipeline (the only place it WAS
    /// registered was Startup.cs, which Program.cs never called -- dead
    /// code registering a dead DbContext).
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<OutageRecord> Outages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // A given facility/generator only has one real record per day.
            // This unique index is what makes upsert-on-ingest possible
            // (see OutageIngestionService) -- without it, re-fetching from
            // EIA has no way to know "have I already seen this row."
            modelBuilder.Entity<OutageRecord>()
                .HasIndex(o => new { o.Facility, o.Generator, o.Period })
                .IsUnique();
        }
    }
}
