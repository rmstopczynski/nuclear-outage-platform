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
    /// Scoped, depends on ApplicationDbContext -- same layering pattern as
    /// OutageService (controllers never inject DbContext directly).
    /// </summary>
    public class WatchlistService
    {
        private readonly ApplicationDbContext _context;

        public WatchlistService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<WatchlistItem>> GetForUserAsync(int userId)
        {
            return await _context.WatchlistItems
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.FacilityName)
                .ToListAsync();
        }

        public async Task<bool> IsWatchingAsync(int userId, string facility)
        {
            return await _context.WatchlistItems
                .AnyAsync(w => w.UserId == userId && w.Facility == facility);
        }

        /// <summary>
        /// Returns false (no-op) if the user is already watching this
        /// facility -- the unique index on (UserId, Facility) backs this
        /// up at the DB level too, but checking here avoids a needless
        /// constraint-violation round trip for the common case.
        /// </summary>
        public async Task<bool> AddAsync(int userId, string facility, string facilityName)
        {
            if (await IsWatchingAsync(userId, facility))
                return false;

            _context.WatchlistItems.Add(new WatchlistItem
            {
                UserId = userId,
                Facility = facility,
                FacilityName = facilityName,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveAsync(int userId, string facility)
        {
            var existing = await _context.WatchlistItems
                .SingleOrDefaultAsync(w => w.UserId == userId && w.Facility == facility);
            if (existing == null)
                return false;

            _context.WatchlistItems.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// The personalized filtered view: all outage rows for facilities
        /// this user is watching. Joins against OutageRecord directly
        /// rather than going through OutageService, since this is a
        /// watchlist-specific query shape (filter by a set of facility
        /// codes), not a general outage query.
        /// </summary>
        public async Task<List<OutageRecord>> GetWatchedOutagesAsync(int userId)
        {
            var watchedFacilities = await _context.WatchlistItems
                .Where(w => w.UserId == userId)
                .Select(w => w.Facility)
                .ToListAsync();

            if (watchedFacilities.Count == 0)
                return new List<OutageRecord>();

            return await _context.Outages
                .Where(o => watchedFacilities.Contains(o.Facility))
                .OrderByDescending(o => o.Period)
                .ThenByDescending(o => o.UpdatedAt)
                .ToListAsync();
        }
    }
}
