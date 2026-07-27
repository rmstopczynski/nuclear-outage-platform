using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MVC_EF_Start_8.Models;
using MVC_EF_Start_8.Services;

namespace MVC_EF_Start_8.Controllers
{
    public class HomeController : Controller
    {
        private readonly OutageService _outageService;
        private readonly EiaIngestionService _ingestionService;

        public HomeController(OutageService outageService, EiaIngestionService ingestionService)
        {
            _outageService = outageService;
            _ingestionService = ingestionService;
        }

        public async Task<IActionResult> Index()
        {
            // No longer fetches on page load -- data ingestion is now
            // handled by EiaIngestionBackgroundService on a schedule (see
            // Step 2 in the README). This just reads whatever's in the
            // database.
            var outagesList = await _outageService.GetAllOutagesAsync();
            return View(outagesList);
        }

        public async Task<IActionResult> Read(string? searchFacility)
        {
            List<OutageRecord> outages;

            if (!string.IsNullOrEmpty(searchFacility))
            {
                outages = await _outageService.SearchOutagesAsync(searchFacility);
            }
            else
            {
                outages = await _outageService.GetLatestOutagesAsync(100);
            }

            return View(outages);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(OutageRecord record)
        {
            if (ModelState.IsValid)
            {
                await _outageService.CreateAsync(record);
                return RedirectToAction("Read");
            }
            return View(record);
        }

        public async Task<IActionResult> Update(int id)
        {
            var record = await _outageService.GetByIdAsync(id);
            if (record == null)
                return NotFound();

            return View(record);
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, OutageRecord updatedRecord)
        {
            var result = await _outageService.UpdateAsync(id, updatedRecord);
            if (!result)
                return NotFound();

            return RedirectToAction("Read");
        }

        public async Task<IActionResult> Delete(int id)
        {
            var record = await _outageService.GetByIdAsync(id);
            if (record == null)
                return NotFound();

            return View(record);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var result = await _outageService.DeleteAsync(id);
            if (!result)
                return NotFound();

            return RedirectToAction("Read");
        }

        /// <summary>
        /// Manual trigger for an immediate ingestion run, on top of the
        /// scheduled background job -- useful for demoing without waiting
        /// for the interval, or forcing a refresh right after deploying.
        /// Calls the exact same EiaIngestionService the background
        /// service uses, so there's no separate/duplicate ingestion logic.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RefreshNow()
        {
            int inserted = await _ingestionService.IngestAsync();
            TempData["RefreshMessage"] = $"Ingestion run complete: {inserted} new record(s) added.";
            return RedirectToAction("Read");
        }

        public async Task<IActionResult> DataVisualization()
        {
            var outagesList = await _outageService.GetAllOutagesAsync();
            return View(outagesList);
        }

        /// <summary>
        /// Backs the Data Visualization page's three interactive,
        /// cross-filtering charts. Returns raw (last-30-days) rows with
        /// region already resolved server-side, rather than three fixed
        /// pre-aggregated series -- aggregation now happens client-side
        /// in site.js, recomputed on every click so selecting a day,
        /// facility, or region filters the other two charts. ~30 days
        /// worth of rows is small enough (a few hundred) to ship whole
        /// and filter in the browser instead of round-tripping to the
        /// server on every click.
        /// </summary>
        public async Task<IActionResult> GetChartData()
        {
            var last30Days = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
            var outagesList = (await _outageService.GetAllOutagesAsync())
                .Where(o => o.Period >= last30Days)
                .ToList();

            var records = outagesList
                .Where(o => o.Outage.HasValue && o.Outage.Value > 0 && !string.IsNullOrWhiteSpace(o.FacilityName))
                .Select(o => new
                {
                    period = o.Period.ToString("yyyy-MM-dd"),
                    facility = o.Facility,
                    facilityName = o.FacilityName.Trim(),
                    region = ResolveRegion(o.FacilityName.Trim()),
                    outage = o.Outage!.Value
                })
                .OrderBy(r => r.period)
                .ToList();

            return Json(new { windowDays = 30, records });
        }

        /// <summary>
        /// Looks up a facility's grid region. Tries an exact (case-
        /// insensitive) match first, then a bidirectional substring check
        /// -- the previous version only checked
        /// <c>rawName.Contains(mapKey)</c>, which silently never matched
        /// when the map's keys were longer than what EIA actually sends
        /// (e.g. rawName "Cooper" can't contain "Cooper Nuclear Station").
        /// Checking both directions, plus keying the map on EIA's actual
        /// short names (see FacilityRegionMap), is what actually fixed the
        /// "everything says Unknown" bug rather than papering over it.
        /// </summary>
        private static string ResolveRegion(string rawName)
        {
            foreach (var kvp in FacilityRegionMap.Regions)
            {
                if (string.Equals(kvp.Key, rawName, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            foreach (var kvp in FacilityRegionMap.Regions)
            {
                if (rawName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains(rawName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return "Other";
        }

        public IActionResult About() => View();
    }
}
