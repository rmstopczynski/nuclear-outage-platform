using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVC_EF_Start_8.Models;
using Newtonsoft.Json;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Fetches outage data from the EIA API and upserts it via
    /// OutageService. Extracted out of HomeController (where this logic
    /// originally lived, tangled up with the request pipeline) so both
    /// the scheduled background service and the manual "Refresh Now"
    /// endpoint call the same code -- one place that knows how to talk to
    /// EIA, not two.
    /// </summary>
    public class EiaIngestionService
    {
        private readonly OutageService _outageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EiaIngestionService> _logger;
        private readonly string _apiPath;

        public EiaIngestionService(
            OutageService outageService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<EiaIngestionService> logger)
        {
            _outageService = outageService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

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

        /// <summary>
        /// Fetches the latest data from EIA and upserts it. Safe to call
        /// repeatedly -- OutageService.UpsertFromEiaAsync only inserts
        /// genuinely new rows, so re-running this doesn't duplicate data.
        /// Returns the number of new rows inserted.
        /// </summary>
        public async Task<int> IngestAsync()
        {
            _logger.LogInformation("Starting EIA ingestion run...");

            HttpResponseMessage response;
            try
            {
                var client = _httpClientFactory.CreateClient("EIA_API");
                response = await client.GetAsync(_apiPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EIA API request threw an exception.");
                return 0;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("EIA API request failed: {StatusCode}", response.StatusCode);
                return 0;
            }

            string body = await response.Content.ReadAsStringAsync();
            EiaRoot? apiData = JsonConvert.DeserializeObject<EiaRoot>(body);

            if (apiData?.response?.data == null)
            {
                _logger.LogWarning("EIA API returned null or empty data.");
                return 0;
            }

            int inserted = await _outageService.UpsertFromEiaAsync(apiData.response.data);
            _logger.LogInformation("EIA ingestion run complete: {Count} new records inserted.", inserted);
            return inserted;
        }
    }
}
