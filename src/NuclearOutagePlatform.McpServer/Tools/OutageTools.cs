using System.ComponentModel;
using Microsoft.Extensions.Http;
using ModelContextProtocol.Server;

namespace NuclearOutagePlatform.McpServer.Tools;

/// <summary>
/// The two tools this MCP server exposes (see the plan's "1-2 tools,
/// well-understood" scope). Both are thin wrappers over the existing
/// GET /api/outages and GET /api/outages/trends endpoints from Step 4 --
/// this class does no data access of its own, just query-string building
/// and passing the response straight through. `IHttpClientFactory` is
/// resolved from the DI container the SDK builds tool methods against
/// (registered in Program.cs), the same way MonkeyService is injected in
/// the official SDK's own sample -- MCP tool parameters that match a
/// registered service type are injected, not supplied by the calling LLM.
/// </summary>
[McpServerToolType]
public static class OutageTools
{
    [McpServerTool]
    [Description(
        "Get recent U.S. nuclear power plant outage records, optionally filtered to one facility. " +
        "Returns raw outage records: facility code/name, generator, period (date), capacity (MW), " +
        "outage (MW), and percent outage.")]
    public static async Task<string> GetRecentOutages(
        IHttpClientFactory httpClientFactory,
        [Description("Facility short code or name to filter by, e.g. '8055' or 'Arkansas'. Omit for all facilities.")]
        string? facility = null,
        [Description("How many days back to look, from today. Defaults to 7.")]
        int days = 7)
    {
        var client = httpClientFactory.CreateClient("NuclearApi");
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-Math.Max(days, 1));

        var query = $"api/outages?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&pageSize=100";
        if (!string.IsNullOrWhiteSpace(facility))
            query += $"&facility={Uri.EscapeDataString(facility)}";

        return await FetchAsync(client, query, "outage");
    }

    [McpServerTool]
    [Description(
        "Get the daily total nuclear outage megawatt (MW) trend for a date range, optionally scoped " +
        "to one facility. Useful for spotting whether outage activity is increasing or decreasing over time.")]
    public static async Task<string> GetGenerationTrend(
        IHttpClientFactory httpClientFactory,
        [Description("Facility short code or name to filter by. Omit for all facilities combined.")]
        string? facility = null,
        [Description("How many days back to look, from today. Defaults to 30.")]
        int days = 30)
    {
        var client = httpClientFactory.CreateClient("NuclearApi");
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-Math.Max(days, 1));

        var query = $"api/outages/trends?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(facility))
            query += $"&facility={Uri.EscapeDataString(facility)}";

        return await FetchAsync(client, query, "trends");
    }

    private static async Task<string> FetchAsync(HttpClient client, string query, string label)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(query);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Most likely cause: the main app (docker compose up) isn't
            // running, or NUCLEAR_API_BASE_URL points somewhere
            // unreachable. Returned as tool output text -- an LLM client
            // reading this can tell the person what's wrong, rather than
            // the tool call just failing opaquely.
            return $"Error: could not reach the Nuclear Outage Platform API to fetch {label} data " +
                   $"({ex.GetType().Name}: {ex.Message}). Is the app running?";
        }

        if (!response.IsSuccessStatusCode)
            return $"Error: the {label} API returned HTTP {(int)response.StatusCode}.";

        return await response.Content.ReadAsStringAsync();
    }
}
