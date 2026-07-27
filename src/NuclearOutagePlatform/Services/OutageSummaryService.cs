using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MVC_EF_Start_8.Models;
using Newtonsoft.Json;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// The one AI feature in this project (see README's "Why AI stays
    /// minimal here" section) -- turns a range of structured outage rows
    /// into a short, plain-English summary a non-technical stakeholder can
    /// skim. Deliberately narrow: one bounded Groq API call over data this
    /// app already has, not a chat feature, not free-form Q&amp;A.
    ///
    /// Scoped (depends on OutageService, which depends on the DbContext),
    /// same layering as every other service here.
    /// </summary>
    public class OutageSummaryService
    {
        private readonly OutageService _outageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OutageSummaryService> _logger;

        private const int MaxRecordsConsidered = 300;

        public OutageSummaryService(
            OutageService outageService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<OutageSummaryService> logger)
        {
            _outageService = outageService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Builds a plain-English summary of outage activity for the given
        /// facility/date-range filter (same filter shape as
        /// GET /api/outages). Returns the summary text and how many
        /// records it was based on.
        /// </summary>
        /// <exception cref="AiSummaryNotConfiguredException">
        /// No Groq API key is configured -- this is an optional feature,
        /// not a hard dependency, so this is checked here rather than at
        /// app startup (contrast with Jwt:Key, which the app can't run
        /// without at all).
        /// </exception>
        /// <exception cref="AiProviderUnavailableException">
        /// The Groq API call itself failed (network error, timeout,
        /// non-success response, or an unparsable response).
        /// </exception>
        public async Task<(string Summary, int RecordCount)> GenerateSummaryAsync(
            string? facility,
            DateOnly? from,
            DateOnly? to)
        {
            var apiKey = _configuration["Groq:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AiSummaryNotConfiguredException(
                    "AI summary feature is not configured. Set Groq:ApiKey " +
                    "(GROQ__APIKEY environment variable / GROQ_API_KEY in .env) to enable it.");
            }

            var effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
            var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var (records, totalCount) = await _outageService.QueryAsync(
                facility, effectiveFrom, effectiveTo, page: 1, pageSize: MaxRecordsConsidered);

            if (records.Count == 0)
            {
                return ("No outage activity was reported for the selected facility/date range.", 0);
            }

            var facilitySummaries = BuildFacilitySummaries(records);
            var prompt = BuildUserPrompt(facilitySummaries, effectiveFrom, effectiveTo, totalCount);

            string summary;
            try
            {
                summary = await CallGroqAsync(apiKey, prompt);
            }
            catch (AiProviderUnavailableException)
            {
                throw; // already logged / well-formed, just propagate
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error building or calling the AI summary.");
                throw new AiProviderUnavailableException("The AI summary provider is temporarily unavailable.", ex);
            }

            return (summary, records.Count);
        }

        private static List<(string FacilityName, int RecordCount, decimal TotalOutage, bool HasActiveOutage)>
            BuildFacilitySummaries(List<OutageRecord> records)
        {
            return records
                .GroupBy(r => string.IsNullOrWhiteSpace(r.FacilityName) ? r.Facility : r.FacilityName)
                .Select(g => (
                    FacilityName: g.Key,
                    RecordCount: g.Count(),
                    TotalOutage: g.Sum(r => r.Outage ?? 0m),
                    HasActiveOutage: g.Any(r => (r.Outage ?? 0m) > 0m)))
                .OrderByDescending(f => f.TotalOutage)
                .Take(20) // keep the prompt small; top 20 facilities by outage MW is plenty for a summary
                .ToList();
        }

        private static string BuildUserPrompt(
            List<(string FacilityName, int RecordCount, decimal TotalOutage, bool HasActiveOutage)> facilitySummaries,
            DateOnly from,
            DateOnly to,
            int totalRecordCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Date range: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
            sb.AppendLine($"Total outage records in range: {totalRecordCount}");
            sb.AppendLine("Per-facility totals (top facilities by outage MW):");

            foreach (var f in facilitySummaries)
            {
                sb.AppendLine(
                    $"- {f.FacilityName}: {f.RecordCount} record(s), " +
                    $"total outage {f.TotalOutage:0.##} MW, " +
                    $"currently reporting an active outage: {(f.HasActiveOutage ? "yes" : "no")}");
            }

            return sb.ToString();
        }

        private async Task<string> CallGroqAsync(string apiKey, string userPrompt)
        {
            var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content =
                            "You summarize U.S. nuclear power plant outage data for a non-technical " +
                            "stakeholder. Write 2-4 plain-English sentences. Only state facts present " +
                            "in the data provided -- never invent facilities, causes, or numbers. Do not " +
                            "speculate about why an outage happened (scheduled vs. unplanned) unless the " +
                            "data says so explicitly, since it doesn't.",
                    },
                    new { role = "user", content = userPrompt },
                },
                max_tokens = 220,
                temperature = 0.3,
            };

            var client = _httpClientFactory.CreateClient("Groq_API");

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Groq API request failed (network/timeout).");
                throw new AiProviderUnavailableException("Could not reach the AI summary provider.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Groq API returned {StatusCode}: {Body}", response.StatusCode, body);
                throw new AiProviderUnavailableException(
                    $"The AI summary provider returned an error ({(int)response.StatusCode}).");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            GroqChatResponse? parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<GroqChatResponse>(responseBody);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse Groq API response.");
                throw new AiProviderUnavailableException("Received an unreadable response from the AI summary provider.", ex);
            }

            var content = parsed?.choices?.FirstOrDefault()?.message?.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Groq API response had no summary content.");
                throw new AiProviderUnavailableException("The AI summary provider returned an empty response.");
            }

            return content.Trim();
        }

        // Minimal shape of the Groq (OpenAI-compatible) chat completions response -- only
        // what this feature actually reads, not the full schema.
        private class GroqChatResponse
        {
            public List<GroqChoice>? choices { get; set; }
        }

        private class GroqChoice
        {
            public GroqMessage? message { get; set; }
        }

        private class GroqMessage
        {
            public string? content { get; set; }
        }
    }
}
