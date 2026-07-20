using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MVC_EF_Start_8.DataAccess;
using MVC_EF_Start_8.Models;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Replaces the original NuclearOutageService, which held everything in
    /// a plain `List&lt;OutageRecord&gt;` inside a Singleton -- meaning every
    /// app restart silently wiped all data back to empty. This version
    /// persists to Postgres via ApplicationDbContext.
    ///
    /// Lifetime note: this service is registered as Scoped, not Singleton
    /// (see Program.cs). EF Core's DbContext is Scoped by default and is
    /// NOT thread-safe / not meant to be held for the app's entire
    /// lifetime -- injecting a Scoped DbContext into a true Singleton is a
    /// well-known ASP.NET Core foot-gun (works until concurrent requests
    /// start corrupting each other's queries). The original Singleton
    /// registration only "worked" because it never actually held a
    /// DbContext at all.
    /// </summary>
    public class OutageService
    {
        private readonly ApplicationDbContext _context;

        public OutageService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasAnyDataAsync()
        {
            return await _context.Outages.AnyAsync();
        }

        public async Task<List<OutageRecord>> GetAllOutagesAsync()
        {
            return await _context.Outages
                .OrderByDescending(o => o.Period)
                .ThenByDescending(o => o.UpdatedAt)
                .ToListAsync();
        }

        public async Task<List<OutageRecord>> GetLatestOutagesAsync(int count)
        {
            return await _context.Outages
                .OrderByDescending(o => o.Period)
                .ThenByDescending(o => o.UpdatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<OutageRecord>> SearchOutagesAsync(string query)
        {
            return await _context.Outages
                .Where(o => o.Facility.Contains(query) || o.FacilityName.Contains(query))
                .OrderByDescending(o => o.Period)
                .Take(100)
                .ToListAsync();
        }

        public async Task<OutageRecord?> GetByIdAsync(int id)
        {
            return await _context.Outages.FindAsync(id);
        }

        /// <summary>
        /// Upserts a batch of freshly-fetched EIA records. Existing rows
        /// (matched on the Facility+Generator+Period unique index) are
        /// left alone; only genuinely new rows get inserted. This replaces
        /// the original "IsDataFetched" boolean flag, which just prevented
        /// re-fetching within a single (in-memory, ephemeral) process run --
        /// with real persistence, the actual question is "which of these
        /// rows are new," which this answers properly.
        /// </summary>
        public async Task<int> UpsertFromEiaAsync(IEnumerable<EiaOutageDto> dtos)
        {
            var existingKeys = await _context.Outages
                .Select(o => new { o.Facility, o.Generator, o.Period })
                .ToListAsync();
            var existingKeySet = existingKeys
                .Select(k => (k.Facility, Generator: k.Generator ?? "", k.Period))
                .ToHashSet();

            var newRecords = new List<OutageRecord>();

            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.facility) || string.IsNullOrWhiteSpace(dto.period))
                    continue;

                if (!DateOnly.TryParse(dto.period, out var period))
                    continue;

                var generator = dto.generator ?? "";
                var key = (dto.facility, generator, period);
                if (existingKeySet.Contains(key))
                    continue;

                newRecords.Add(new OutageRecord
                {
                    Period = period,
                    Facility = dto.facility,
                    FacilityName = dto.facilityName ?? "",
                    Generator = dto.generator,
                    Capacity = ParseDecimal(dto.capacity),
                    Outage = ParseDecimal(dto.outage),
                    PercentOutage = ParseDecimal(dto.percentOutage),
                });

                // Prevent duplicate inserts within the same batch too, not
                // just against what's already in the DB.
                existingKeySet.Add(key);
            }

            if (newRecords.Count > 0)
            {
                _context.Outages.AddRange(newRecords);
                await _context.SaveChangesAsync();
            }

            return newRecords.Count;
        }

        public async Task<OutageRecord> CreateAsync(OutageRecord record)
        {
            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            _context.Outages.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<bool> UpdateAsync(int id, OutageRecord updated)
        {
            var existing = await _context.Outages.FindAsync(id);
            if (existing == null)
                return false;

            existing.Period = updated.Period;
            existing.Facility = updated.Facility;
            existing.FacilityName = updated.FacilityName;
            existing.Generator = updated.Generator;
            existing.Capacity = updated.Capacity;
            existing.Outage = updated.Outage;
            existing.PercentOutage = updated.PercentOutage;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.Outages.FindAsync(id);
            if (existing == null)
                return false;

            _context.Outages.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        private static decimal? ParseDecimal(string? value)
        {
            return decimal.TryParse(value, out var result) ? result : null;
        }
    }
}
