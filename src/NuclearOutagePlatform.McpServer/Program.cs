using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Stdio transport means stdout IS the MCP protocol channel -- anything an
// MCP host (Claude Desktop, VS Code, the Inspector CLI) reads from this
// process's stdout has to be valid protocol frames. All logging must go
// to stderr instead, or a stray log line corrupts the stream and the
// client just sees the connection hang (a real failure mode documented in
// the official SDK's own getting-started guide).
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// This server doesn't talk to Postgres or OutageService directly -- it's
// a thin translation layer over the REST API already built in Step 4.
// That's a deliberate design choice: MCP adds structured tool discovery
// and typed schemas on top of REST, it doesn't replace the data-access
// layer. Defaults to the local docker-compose app's exposed port;
// overridable via NUCLEAR_API_BASE_URL if this is ever pointed at a
// different deployment (e.g. Render, once Step 8 -- if it ever happens --
// gives the app a real public URL).
var apiBaseUrl = Environment.GetEnvironmentVariable("NUCLEAR_API_BASE_URL") ?? "http://localhost:8090/";

builder.Services.AddHttpClient("NuclearApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
