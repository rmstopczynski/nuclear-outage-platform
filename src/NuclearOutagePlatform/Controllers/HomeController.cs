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

        public async Task<IActionResult> GetChartData()
        {
            var outagesList = await _outageService.GetAllOutagesAsync();
            var facilityRegionMap = FacilityRegionMap.Regions;

            var dailyOutageMap = new Dictionary<DateOnly, decimal>();
            var generatorOutageMap = new Dictionary<string, decimal>();
            var generatorFrequencyMap = new Dictionary<string, int>();

            foreach (var outage in outagesList)
            {
                if (!outage.Outage.HasValue || outage.Outage.Value <= 0)
                    continue;
                if (string.IsNullOrWhiteSpace(outage.FacilityName))
                    continue;

                dailyOutageMap[outage.Period] = dailyOutageMap.GetValueOrDefault(outage.Period) + outage.Outage.Value;

                string rawName = outage.FacilityName.Trim();
                string region = facilityRegionMap.TryGetValue(rawName, out var exactRegion)
                    ? exactRegion
                    : facilityRegionMap.FirstOrDefault(kvp => rawName.Contains(kvp.Key)).Value ?? "Unknown";

                string label = $"{rawName} ({region})";
                generatorOutageMap[label] = generatorOutageMap.GetValueOrDefault(label) + outage.Outage.Value;
                generatorFrequencyMap[label] = generatorFrequencyMap.GetValueOrDefault(label) + 1;
            }

            var last30Days = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
            var sortedDailyOutages = dailyOutageMap
                .Where(kv => kv.Key >= last30Days)
                .OrderBy(kv => kv.Key)
                .ToList();

            var sortedTopGeneratorOutages = generatorOutageMap
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToList();
            var sortedTopGeneratorFrequencies = generatorFrequencyMap
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToList();

            var response = new
            {
                dailyOutages = new
                {
                    labels = sortedDailyOutages.Select(kv => kv.Key.ToString("yyyy-MM-dd")).ToList(),
                    values = sortedDailyOutages.Select(kv => kv.Value).ToList()
                },
                generatorOutages = new
                {
                    labels = sortedTopGeneratorOutages.Select(kv => kv.Key).ToList(),
                    values = sortedTopGeneratorOutages.Select(kv => kv.Value).ToList()
                },
                generatorFrequency = new
                {
                    labels = sortedTopGeneratorFrequencies.Select(kv => kv.Key).ToList(),
                    values = sortedTopGeneratorFrequencies.Select(kv => kv.Value).ToList()
                }
            };

            return Json(response);
        }

        public IActionResult About() => View();
    }
}
