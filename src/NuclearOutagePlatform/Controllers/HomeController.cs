using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVC_EF_Start_8.Models;
using MVC_EF_Start_8.Services;
using Newtonsoft.Json;

namespace MVC_EF_Start_8.Controllers
{
    public class HomeController : Controller
    {
        private readonly OutageService _outageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HomeController> _logger;
        private readonly string _apiPath;

        public HomeController(
            OutageService outageService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<HomeController> logger)
        {
            _outageService = outageService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // API key now comes from configuration (env var / user-secrets /
            // Render environment variable), never hardcoded in source. See
            // README's "Secrets management" section for the full story --
            // the original had this key as a `const string` directly in
            // this file, in addition to being leaked in the README.
            var apiKey = configuration["Eia:ApiKey"]
                ?? throw new InvalidOperationException(
                    "EIA API key not configured. Set the Eia:ApiKey configuration value " +
                    "(EIA__APIKEY environment variable, or dotnet user-secrets in local dev).");

            _apiPath = "nuclear-outages/generator-nuclear-outages/data/" +
                "?frequency=daily" +
                "&data[0]=capacity&data[1]=outage&data[2]=percentOutage" +
                "&sort[0][column]=period&sort[0][direction]=desc" +
                "&offset=0&length=5000" +
                $"&api_key={apiKey}";
        }

        private async Task RefreshFromEiaIfEmptyAsync()
        {
            if (await _outageService.HasAnyDataAsync())
                return;

            _logger.LogInformation("No data in database yet -- fetching from EIA API...");

            var client = _httpClientFactory.CreateClient("EIA_API");
            HttpResponseMessage response = await client.GetAsync(_apiPath);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EIA API request failed: {StatusCode}", response.StatusCode);
                return;
            }

            string body = await response.Content.ReadAsStringAsync();
            EiaRoot? apiData = JsonConvert.DeserializeObject<EiaRoot>(body);

            if (apiData?.response?.data == null)
            {
                _logger.LogWarning("EIA API returned null or empty data.");
                return;
            }

            int inserted = await _outageService.UpsertFromEiaAsync(apiData.response.data);
            _logger.LogInformation("Inserted {Count} new outage records.", inserted);
        }

        public async Task<IActionResult> Index()
        {
            await RefreshFromEiaIfEmptyAsync();
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

        public async Task<IActionResult> DataVisualization()
        {
            await RefreshFromEiaIfEmptyAsync();
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
