# U.S. Nuclear Outage Tracking Platform

An ASP.NET Core MVC application tracking U.S. nuclear power plant outages,
built on live data from the [EIA (U.S. Energy Information Administration)
API](https://www.eia.gov/opendata/). Originally a group project for a
university course (ISM6225); this repo is a solo continuation and rebuild,
starting from that codebase but under new, independent ownership.

## Origin and rebuild scope

The original group project is preserved at
[mb-acosta/ISM6225-Dynamic-Web-App-Final-Project](https://github.com/mb-acosta/ISM6225-Dynamic-Web-App-Final-Project).
This repo starts fresh from that code rather than continuing to build on
the fork, for two reasons: it's now solo work, not group work, and it
deserves its own commit history; and the original relied on
school-provided Azure access that no longer exists post-graduation (the
live deployment is gone, and re-hosting it means re-architecting the
infrastructure anyway — see below).

**What auditing the inherited code actually found**, before any new
features got added:

- **There was no real database.** All "persistence" was a plain
  `List<OutageRecord>` held in memory inside a Singleton service. Every
  server restart silently wiped all data back to empty. The EF Core /
  `ApplicationDbContext` code that *looked* like real persistence only
  had `DbSet<Company>` and `DbSet<Quote>` — leftover scaffold from an
  unrelated stock-quote tutorial template, never actually connected to
  the real app (the only place it was even registered, `Startup.cs`,
  was dead code itself — `Program.cs` uses the modern minimal-hosting
  API and never references `Startup` at all).
- **Three hardcoded credentials**, committed in plain text: the EIA API
  key (as a `const string` directly in `HomeController.cs`, and also
  leaked in the README), a local SQL Server password, and an Azure SQL
  password — all in `appsettings.json`.
- **A real data-modeling bug.** Update/Delete matched records by
  `facility` name alone. Since this is daily time-series data, one
  facility has many records — one per day. Editing or deleting "a
  facility's outage" could silently grab the wrong day's record, because
  there was no actual unique key.
- **A `Singleton` holding logic meant for `Scoped` lifetime** — not a bug
  yet (since the "database" was a harmless in-memory list), but it would
  have become a real bug the moment a real `DbContext` got added, since
  `DbContext` isn't thread-safe and a `Singleton` is shared across every
  concurrent request. Worth listing because it's exactly the kind of
  thing that looks fine in a demo and breaks under real concurrent load.
- **`HttpClient` created directly** (`new HttpClient()`) inside the
  controller instead of via the already-registered `IHttpClientFactory`
  — a well-known .NET anti-pattern (socket exhaustion under load), and
  the DI registration for it existed but was simply never used.
- A duplicated `PackageReference` for `Newtonsoft.Json` in the `.csproj`
  (the exact same line, twice) — harmless, but a sign nothing had been
  cleaned up since initial scaffolding.

None of this is a knock on the original group project — it was built
under a semester deadline as a class assignment, and it did what it
needed to do for that. It's listed here because *finding and fixing all
of this* is the actual engineering story of this rebuild, and worth being
concrete about rather than vague.

## Why Postgres instead of Azure SQL

The original used Azure SQL via school-provided Azure credits that no
longer exist. Two options: pay for Azure out of pocket, or self-host a
free alternative — same reasoning as the substitutions in [the healthcare
data engineering platform](https://github.com/rmstopczynski/healthcare-data-engineering-platform). SQL Server itself can run free in Docker
(Developer edition), which would have meant zero code changes — but real
hosting platforms with a genuine free tier (see below) don't have the
RAM headroom to run SQL Server reliably, only Postgres. Rather than
maintain two different databases (SQL Server locally, something else in
production), this uses Postgres everywhere: local dev via Docker Compose,
and the same self-hosted pattern in production.

## Why Render instead of Azure App Service

The original deployed to Azure App Service via student credits. Render
has a genuine free tier — no credit card at signup, deploys straight
from a Dockerfile. The honest trade-off: free web services on Render
spin down after 15 minutes of inactivity and take 30-60 seconds to wake
back up on the next request. Fine for a portfolio demo link, not
something you'd want for a real product — worth saying plainly if asked.

## What's in this step (Step 1: Foundation)

- Real Postgres persistence via EF Core, replacing the in-memory
  Singleton `List<T>` — data now survives a restart
- A proper primary key (`Id`) on the outage entity, fixing the
  Update/Delete matching-the-wrong-record bug
- A clean DTO/entity separation: `EiaOutageDto` absorbs the EIA API's
  raw (all-string) JSON shape; `OutageRecord` is the real, properly
  typed domain entity (`DateOnly`, `decimal?` instead of everything
  being a string)
- Idempotent upsert-on-ingest: re-fetching from EIA only inserts
  genuinely new rows (matched on a `Facility + Generator + Period`
  unique index), instead of re-fetching being gated by an in-memory
  boolean flag that reset on every restart anyway
- All three hardcoded credentials removed — API key and DB connection
  string now come from environment variables / configuration, never
  committed
- Fixed the `Singleton`-holding-`Scoped`-dependency issue (service is
  now `Scoped`, matching `DbContext`'s actual lifetime)
- Fixed the `HttpClient` anti-pattern — controller now uses the
  already-registered `IHttpClientFactory` instead of constructing its
  own client
- Dead code removed: `Startup.cs`, the `Company`/`Quote`/`ChartRoot`
  tutorial scaffold, the duplicate `Newtonsoft.Json` package reference
- Docker Compose for local dev (Postgres + the app), matching the
  self-hosted pattern used throughout the healthcare pipeline project

**Deliberately NOT in this step** (planned for later, see Roadmap): auth,
user accounts, watchlists, scheduled background ingestion (data still
refreshes on page load if the table is empty, same trigger as before —
just persisted now), search/filter UI improvements, CI/CD, React
frontend, live deployment. Keeping each step's diff reviewable on its
own, same discipline as the other project. (For what it's worth: most of
this list *did* get built in later steps — auth/watchlists in Step 3,
background ingestion in Step 2, search/filter folded into Step 4's API
query params. CI, React, and live deployment did not, per the roadmap
pivot described below.)

## Quickstart

Requires [Docker Desktop](https://www.docker.com/products/docker-desktop/)
and a free EIA API key ([register here](https://www.eia.gov/opendata/register.php)).

```bash
cp .env.example .env
# edit .env, set EIA_API_KEY to your real key

docker compose up -d --build
```

Then open `http://localhost:8090`. First load will fetch live data from
EIA and populate Postgres (this can take a few seconds — up to 5000
records depending on EIA's current data volume).

Port note: this project maps to `8090` (app) and `5433` (Postgres) on
the host rather than the more obvious `8080`/`5432`, specifically so it
can run alongside the healthcare data pipeline project without either
one needing to be stopped first — both projects' containers can be up
at the same time. Internally, nothing changes — the app still listens
on `8080` and Postgres on `5432` inside their own containers; only the
host-side port numbers differ.

## Repo structure

```
├── docker-compose.yml
├── .env.example
└── src/
    └── NuclearOutagePlatform/
        ├── Dockerfile
        ├── NuclearOutagePlatform.csproj
        ├── Program.cs
        ├── appsettings.json
        ├── Controllers/
        │   ├── HomeController.cs
        │   ├── AuthController.cs
        │   ├── WatchlistController.cs
        │   └── Api/
        │       └── OutagesApiController.cs   # REST API (Step 4)
        ├── DataAccess/
        │   └── ApplicationDbContext.cs
        ├── Models/
        │   ├── OutageRecord.cs       # real EF Core entity
        │   ├── EiaOutageDto.cs        # raw EIA API response shape
        │   ├── ChartDataViewModel.cs
        │   ├── FacilityRegionMap.cs
        │   ├── User.cs, WatchlistItem.cs
        │   ├── RegisterViewModel.cs, LoginViewModel.cs
        │   └── Api/
        │       ├── OutageDto.cs, OutageWriteRequest.cs
        │       ├── PagedResult.cs
        │       ├── TrendResponseDto.cs
        │       └── OutageSummaryResponseDto.cs
        ├── Services/
        │   ├── OutageService.cs      # DB-backed, replaces the in-memory Singleton
        │   ├── EiaIngestionService.cs, EiaIngestionBackgroundService.cs
        │   ├── AuthService.cs, PasswordHasher.cs
        │   ├── WatchlistService.cs
        │   └── OutageSummaryService.cs, AiSummaryExceptions.cs
        ├── Views/
        └── wwwroot/
    └── NuclearOutagePlatform.McpServer/    # MCP server (Step 6)
        ├── NuclearOutagePlatform.McpServer.csproj
        ├── Program.cs                      # stdio transport, tool registration
        └── Tools/
            └── OutageTools.cs               # get_recent_outages, get_generation_trend
```

## Roadmap

This roadmap was revised partway through the project. Steps 1-2 were built
under a generic "keep adding full-stack features" plan (search/filter, CI,
a React frontend, live deployment). Starting at Step 4, the project follows
a more targeted plan aimed at the specific backend/software-engineering
roles this portfolio is for: a real REST API layer, one small,
well-justified AI feature, and an optional MCP server — deliberately
*not* a React rewrite or a CI pipeline, since those don't add to the
specific story this project is telling. Step 3 (auth/watchlists) was
already built before the pivot and is kept as-is; it isn't required by the
new plan but isn't in conflict with it either.

1. ✅ **Foundation** — real persistence, secrets cleanup, bug fixes, Docker
2. ✅ **Scheduled background ingestion** — `BackgroundService` running for the
   app's lifetime, replacing fetch-on-page-load
3. ✅ **JWT authentication, user accounts, watchlists**
4. ✅ **REST API layer** — CRUD, facility/date-range filters, trend
   aggregation, proper DTOs and pagination
5. ✅ **Small AI-assisted outage summary** — bounded LLM API call (Groq,
   OpenAI-compatible) over structured data, not a chat feature
6. ✅ **MCP server** — exposes outage data as two tools over stdio
7. ✅ **Polish + documentation** — README pass and defense notes done (this
   step); fresh screenshots/demo GIF still pending

Dropped from the original plan (not being built): search/filter as a
separate step (folded into Step 4's API filters instead), GitHub Actions
CI, a React frontend, and live deployment to Render. None of these serve
the project's current, more targeted story; they could be revisited later
if the goals change again.

## Step 2: Scheduled background ingestion

Replaced the "fetch from EIA when the page loads and the table looks
empty" trigger with a real `BackgroundService` (`EiaIngestionBackgroundService`)
that runs for the app's entire lifetime: once ~15 seconds after startup,
then every `Eia:IngestionIntervalHours` (default 6, configurable via
`.env`). The EIA-fetching logic itself, which used to live tangled up
inside `HomeController`'s constructor, is now its own
`EiaIngestionService` — one place that knows how to talk to EIA, shared
by both the scheduled job and a manual **"Refresh Now"** button added to
the Outages page for on-demand runs without waiting for the interval.

Confirmed working end to end: the automatic startup run found and
inserted 384 genuinely new records the first time it ran; every run
since (both scheduled and manual) correctly found 0 new records, proving
the idempotent upsert logic (`OutageService.UpsertFromEiaAsync`, from
Step 1) works as intended rather than re-inserting duplicates.

**Lifetime handling, done correctly from the start:** `BackgroundService`
instances are Singletons, but `EiaIngestionService` (and the `DbContext`
underneath it) are Scoped. `EiaIngestionBackgroundService` creates a
fresh DI scope via `IServiceScopeFactory` on every run rather than
holding one long-lived instance — avoiding a repeat of the exact
Singleton-holding-Scoped-dependency bug documented in Step 1's audit.

## Step 3: JWT authentication, user accounts, watchlists

Added real accounts on top of the existing outage-tracking features:
register/login with a JWT stored in an **HttpOnly cookie** (not a bearer
header — this is a server-rendered MVC app with full-page navigations, not
a JS client that could attach an `Authorization` header on every
navigation). ASP.NET Core's JWT Bearer handler is configured with a custom
`OnMessageReceived` event that pulls the token out of the cookie instead of
expecting the header. The same scheme would accept a real `Authorization:
Bearer` header from a future SPA client with zero rearchitecture.

Password hashing is hand-rolled PBKDF2 via `Rfc2898DeriveBytes` (random
salt, 100k iterations, SHA256, timing-safe comparison), not
`Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` — deliberately
avoiding pulling in the full Identity membership system for something this
scoped. Stored as `"{iterations}.{salt}.{hash}"` so the iteration count can
be raised later without invalidating existing hashes.

The cookie's `Secure` flag is conditional (`Secure = Request.IsHttps`), not
hardcoded `true` — the app runs over plain HTTP in local Docker
(`http://localhost:8090`); a hardcoded `Secure` cookie would silently never
get sent back by the browser there, breaking login locally, while still
becoming properly secure automatically once deployed with real HTTPS.

Also fixed during this step: `app.UseAuthentication()` was missing
entirely from `Program.cs` (only `UseAuthorization()` existed) — without
it, `[Authorize]` has nothing populating the request's identity/claims, so
it wouldn't have worked at all once added.

Users can now follow specific facilities from the Outages page and see a
personalized filtered view at `/Watchlist`.

## Step 4: REST API layer

Added a proper JSON REST API (`Controllers/Api/OutagesApiController.cs`,
routed at `/api/outages`), distinct from the existing Razor/MVC pages in
`HomeController`. This is the piece the project's current plan calls for
as its own architecture box, separate from the web dashboard — something a
script, another service, or (in Steps 5-6) the AI summary feature or an
MCP server can call directly instead of going through HTML pages.

- `GET /api/outages` — paged, filterable by `facility` (matches short code
  or full name) and a `from`/`to` date range
- `GET /api/outages/{id}`
- `GET /api/outages/trends` — daily total outage MW, optionally scoped to
  one facility and/or a date range
- `POST` / `PUT /{id}` / `DELETE /{id}` — full CRUD, with dedicated
  request/response DTOs (`OutageWriteRequest`, `OutageDto`) rather than
  binding directly to the EF entity, to avoid over-posting (a client
  shouldn't be able to set `Id`/`CreatedAt` on create)

Validation and error handling: `[ApiController]` gives automatic 400
responses for invalid input (missing required fields, out-of-range
values via `[Range]`); unexpected failures are caught, logged via
`ILogger`, and returned as a 500 `ProblemDetails` instead of leaking a raw
exception. Read endpoints are anonymous, matching the existing MVC pages'
permissions; write endpoints are anonymous too for now, same as the
existing MVC Create/Update/Delete actions — not a new gap introduced here,
just not yet tightened (a reasonable next hardening step, out of scope for
"add the API layer").

## Step 5: AI-assisted outage summary

Added the one AI feature in this project: `GET /api/outages/summary`,
which pulls the same filtered outage data `GET /api/outages/trends` uses,
aggregates it per facility, and asks an LLM for a short, plain-English
summary (2-4 sentences). Surfaced as a small "Generate Summary" card on
the Data Visualization dashboard, not a redesign of the UI, and not a
chat interface.

**Why Groq instead of OpenAI:** built against OpenAI first, then switched
after hitting OpenAI's quota wall (no billing configured on the account).
Groq's API is OpenAI-compatible — identical request/response shape,
identical Bearer-token auth — so the swap was a small, contained change
(base URL, one config section, model name), not a rewrite. Groq also has
a genuinely free tier with no credit card required, which fits this
project's existing "avoid unnecessary paid services" pattern (see the
Postgres-instead-of-Azure-SQL and Render-free-tier reasoning above, and
MinIO-instead-of-AWS in the healthcare project) better than a
pay-as-you-go API would have. This is also a decent interview story on
its own: the code was written against one provider's API and needed only
a config-level change to run against a different, OpenAI-compatible one,
because the provider-specific bits were isolated in one service.

**Why this stays this narrow, if asked:** the core value of this app is
reliable data ingestion, storage, and visualization — an LLM doesn't fix a
problem that doesn't exist in that layer. The one place natural language
legitimately helps is turning a wall of tabular outage data into something
a non-technical stakeholder can skim in one sentence. That's a real,
bounded use case, not AI for its own sake, and it's why this project
deliberately excludes things like RAG, a vector database, or a chat
interface — none of them solve a problem this app actually has.

**Design choices:**
- The prompt is built from an aggregated summary (per-facility totals,
  record counts, whether an outage is currently active), not the raw row
  data — keeps the prompt small and keeps the model from being handed more
  than it needs to answer the one question being asked of it.
- The system prompt explicitly tells the model not to invent facts,
  causes, or numbers beyond what's in the data — the source data has no
  "scheduled vs. unplanned" field, for example, so the model is told not
  to guess at that distinction.
- **No API key is required for the app to start.** `Eia:ApiKey` and
  `Jwt:Key` are hard dependencies the app throws on at startup if missing;
  `Groq:ApiKey` is checked lazily, only when `/api/outages/summary` is
  actually called, and returns a clear `503` ("AI summary feature is not
  configured") rather than an unrelated crash. This is a genuine optional
  feature, and the rest of the app shouldn't depend on whether an external
  AI provider account is fully set up.
- Failures are split into two distinct cases the client can tell apart:
  `503` for "this isn't configured at all," `502` for "it's configured but
  the provider call itself failed" (network error, rate limit, malformed
  response) — same "handle API issues gracefully, don't crash" approach
  already used for the EIA ingestion service in Step 2. This distinction
  is what actually caught the OpenAI billing issue during development: the
  502 path logged the exact upstream error body (a 429 quota error), which
  is what prompted the switch to Groq rather than leaving the feature
  broken.

## Step 6: MCP server

Added a minimal Model Context Protocol (MCP) server
(`src/NuclearOutagePlatform.McpServer/`) exposing two tools over stdio:

- `get_recent_outages(facility?, days=7)` — recent outage records,
  optionally filtered to one facility
- `get_generation_trend(facility?, days=30)` — daily total outage MW
  trend over a date range, optionally scoped to one facility

**What MCP actually is, if asked:** a standardized way for an AI client
(Claude Desktop, an IDE's AI agent, any MCP-aware host) to *discover*
what tools a server offers — names, descriptions, and typed parameter
schemas — and call them, without the client needing hand-written,
model-specific instructions describing your API. Compare that to handing
an LLM your REST API's Swagger docs and hoping it infers the right
call shape: MCP tools are self-describing over a structured protocol the
client already knows how to parse, so a properly-behaved MCP client can
use `get_recent_outages` correctly without ever having seen this project's
code.

**Why this is a thin wrapper, not a second data layer:** the MCP server
does no database access and doesn't reference `OutageService` or
`ApplicationDbContext` at all. Each tool calls the existing
`GET /api/outages` / `GET /api/outages/trends` endpoints from Step 4 over
plain HTTP and returns the response. This was a deliberate choice: it
means there's exactly one place that knows how to query outage data (the
REST API), and MCP is purely an additional, structured way to reach it —
not a parallel implementation that could drift out of sync with the API's
behavior.

**Why stdio, not HTTP transport:** stdio is the standard way a *local*
MCP server gets used — the host application (Claude Desktop, an IDE)
launches it as a child process and talks to it over stdin/stdout, no port
or network configuration needed. The official SDK also supports an HTTP
transport for remote servers, which would matter if this were meant to be
called by a client that isn't running on the same machine — out of scope
here.

**How to test it (matters for defending this — this was actually run, not
just written):**

1. Make sure the main app is running: `docker compose up -d` (the tools
   call its REST API at `http://localhost:8090`).
2. Point an MCP client at this project. For Claude Desktop, add to its
   config file (`%APPDATA%\Claude\claude_desktop_config.json` on Windows):
   ```json
   {
     "mcpServers": {
       "nuclear-outage-platform": {
         "command": "dotnet",
         "args": [
           "run",
           "--project",
           "C:\\path\\to\\nuclear-outage-platform\\src\\NuclearOutagePlatform.McpServer\\NuclearOutagePlatform.McpServer.csproj"
         ]
       }
     }
   }
   ```
   Restart Claude Desktop, and both tools should appear as available. Ask
   it something like *"what nuclear facilities have had outages in the
   last week?"* and it should call `get_recent_outages` and answer from
   the real result.
3. Alternatively, the official test harness works without any AI client
   at all: `npx @modelcontextprotocol/inspector dotnet run --project
   src/NuclearOutagePlatform.McpServer/NuclearOutagePlatform.McpServer.csproj`
   opens a local web UI that lists both tools and lets you call them
   directly with arbitrary arguments — useful for confirming the server
   works end to end without depending on any particular LLM's tool-calling
   behavior.

## Challenges encountered (and how they were resolved)

- **The \"database\" wasn't real.** Confirmed by actually reading
  `ApplicationDbContext` rather than assuming EF Core code that compiles
  and runs means it's connected to anything — `DbSet<Company>` and
  `DbSet<Quote>` had nothing to do with nuclear outages, and grep-ing for
  where `Startup.cs` (the only place that context was registered) was
  actually called turned up nothing, because `Program.cs` never calls
  it. Lesson: "there's a DbContext" and "the app persists data" are not
  the same claim, and it's worth verifying the second one directly.
- **The Update/Delete bug only shows up with real data.** Matching by
  `facility` name works fine in a demo with one row per facility; it
  silently breaks the moment there's more than one day of data for the
  same facility — which is every real use of this app, since it's daily
  time-series data. Caught by reading the actual data shape (one
  facility, many dates) rather than trusting that CRUD "worked" in a
  quick manual test.
- **The XML-comment-`--` bug from Step 1 recurred a third time**, this
  time in a comment added to the `.csproj` for the new JWT Bearer package
  reference ("...packages above -- unverified against NuGet..."). Same
  root cause as before — XML comments can't contain `--` — caught the
  same way: `dotnet restore` failing inside the Docker build with an exact
  line/column number, rather than trusting a comment change couldn't
  possibly break a build.
- **JWT signing key too short for HS256.** `Rfc2898DeriveBytes`/PBKDF2
  password hashing doesn't care about key length, but HMAC-SHA256 (the
  JWT signing algorithm) requires a key of at least 256 bits. An initial
  `JWT_KEY` value in `.env` was only 208 bits, which surfaced as an
  `ArgumentOutOfRangeException` the moment `/Auth/Register` tried to sign
  a token — not at app startup, since the key's length isn't checked until
  a signature is actually computed. Fixed by generating a longer key
  (`openssl rand -base64 48`, comfortably over the 32-byte/256-bit
  minimum).
- **Watchlist "duplicate rows," which weren't actually duplicates.** After
  watching two different generators at the same facility, the watchlist
  page appeared to show repeated rows. This is correct behavior, not a
  bug: a watchlist entry is keyed on facility only, so watching one
  generator effectively watches the whole facility, and the watchlist
  view shows every historical record for that facility (all generators,
  all ingested dates) — a facility with 2 generators and 3 weeks of daily
  data legitimately produces ~2 rows per day. Worth calling out here since
  it's a real design decision (watch a facility, not a specific
  generator/date snapshot) that's easy to mistake for a bug at a glance.
- **OpenAI quota error, pivoted to Groq.** The AI summary feature (Step 5)
  was originally built against OpenAI. The code and the 502 error path
  worked correctly on the first real test — but the 502's logged detail
  showed OpenAI returning a 429 with `"You exceeded your current quota,
  please check your plan and billing details"`: a billing-not-configured
  problem on the account, not a bug in the integration. Rather than pay
  for API credits on a portfolio project, switched to Groq, whose API is
  OpenAI-compatible (same request/response shape, same Bearer auth) and
  has a genuine no-credit-card free tier. Because the provider-specific
  pieces (base URL, config keys, model name) were isolated in one service
  (`OutageSummaryService`), the swap only touched that file, `Program.cs`'s
  named `HttpClient` registration, and config — not the controller, the
  DTOs, or the dashboard UI.
- **MCP server: missing assembly reference, not just a missing `using`.**
  `IHttpClientFactory` failed to resolve in the new MCP server console
  project with `CS0246`. The web app never needs an explicit package
  reference for it because ASP.NET Core's shared framework bundles
  `Microsoft.Extensions.Http` for free; a plain console app
  (`Microsoft.NET.Sdk`, no shared framework) doesn't get that for free and
  needs its own `PackageReference`. Diagnosed by first assuming it was
  just a missing `using` directive (an easy, wrong guess), adding one, and
  getting a more specific `CS0234` ("the namespace doesn't exist in any
  referenced assembly") on the *next* build — which is what actually
  pointed at the real fix (add the package, not just the `using`).
- **MCP Inspector's first connection attempt timed out**, not because
  anything was broken, but because `dotnet run` on a brand-new project has
  to restore NuGet packages and build from scratch before the process can
  even respond to MCP's `initialize` handshake — slower than Inspector's
  connection timeout. The ~30 `notifications/message` log entries visible
  in Inspector's UI during the failed attempt were actually a good sign:
  the server process *had* started and *was* emitting real log output, just
  too slowly for that first handshake. Fixed by running `dotnet build`
  once up front so the subsequent `dotnet run` (which Inspector launches)
  only has to verify an already-current build rather than compile from
  scratch.

## Step 7 (continued): visual design pass

The original UI was leftover styling from the university course this
project started from &mdash; USF green/gold branding, default Bootstrap
card shadows on a stark-white background, Arial, and a Font Awesome icon
reference on the home page that was never actually wired up (no CDN link
existed, so those icons silently never rendered).

Replaced with a design grounded in the actual subject &mdash; a nuclear
plant outage monitor &mdash; rather than a generic dashboard reskin:

- **Dark control-room palette** instead of a bright white background:
  a near-black panel background (`#0E1512`/`#16211C`), off-white text
  (not stark white, less glare), IBM Plex Sans for headings/body and
  IBM Plex Mono for every number, facility code, date, and percentage
  &mdash; reads like an actual telemetry readout instead of prose.
- **Status chips as the signature element**: a green/amber/red dot +
  label (nominal / partial outage / offline) shown consistently on the
  Outages table and Watchlist, borrowed directly from real
  annunciator-panel status lights rather than an arbitrary badge color
  scheme. This also directly answers "the visualizations are ugly" by
  giving the tables actual at-a-glance information structure, not just a
  recolor.
- **Fixed the cramped Update/Delete/Watch buttons** with a real
  `.action-row` flex container (defined gap, not reliance on incidental
  inline whitespace between elements).
- **Rebuilt the Create/Update/Login/Register forms** as proper cards with
  a label/input system (uppercase small-caps labels, monospace inputs,
  visible focus rings) instead of bare unstyled `<div><label><input>`
  stacks.
- **Replaced the stale ERD image** on the About page with a new SVG data
  model diagram reflecting the *current* schema (`User`, `WatchlistItem`,
  `OutageRecord`, with the real FK vs. the app-level facility-code join
  called out explicitly) instead of a pre-Step-3 diagram that predated
  auth entirely.
- **Rewrote the home and About page copy**, which had gone stale after
  Steps 3-6 &mdash; the old copy only described basic CRUD + charts and
  never mentioned the REST API, AI summary, or MCP server that now exist.
- Chart.js configs (in `site.js`) now use the same palette instead of
  Chart.js's default teal/dark-green, and a guard was added so the chart
  setup only runs on pages that actually have the canvases &mdash; it was
  previously running (and silently failing) on every page site-wide.
- **Fixed a real bug behind the "everything says Unknown" complaint**,
  not just a display issue: `FacilityRegionMap`'s keys were long official
  NRC names ("Cooper Nuclear Station"), but `HomeController` matched them
  against EIA's actual short `facilityName` values ("Cooper") with
  `rawName.Contains(mapKey)` &mdash; a short string can never contain a
  longer one, so that match almost never succeeded. Rebuilt the map keyed
  on EIA's real short names, and fixed the matching to check both
  directions, case-insensitively.
- **Replaced the "Frequency of Outages" pie chart** with a facility bar
  chart (top 10 by outage MW) and a region bar chart, both horizontal.
  Pie charts don't work well past 3-4 categories &mdash; comparing slice
  angles is a harder visual task than comparing bar lengths, and thin
  slices are small, fiddly hover targets. Horizontal bars also mean full
  facility names are readable directly on the axis, no truncation needed.
- **Bounded all three charts to the same rolling 30-day window.** The
  facility/region totals previously summed the app's *entire* ingestion
  history with no indication of that in the title, inconsistent with the
  daily trend chart sitting right next to them.
- Improved the daily trend chart's hover behavior (`interaction: {mode:
  'index', intersect: false}`, larger hit radius) so a tooltip appears
  anywhere along a day's vertical slice, not only when the cursor lands
  exactly on a point.
- **Made the three charts cross-filter each other.** Clicking a day,
  facility bar, or region bar filters the other two charts to just that
  selection (click the same one again to clear it). `GET /Home/GetChartData`
  now returns raw per-record rows (region pre-resolved server-side)
  instead of three fixed pre-aggregated series &mdash; a month of data is
  small enough (a few hundred rows) to ship once and re-aggregate
  client-side on every click, rather than round-tripping to the server
  for each interaction. Each chart is filtered by the *other* two
  dimensions, not its own, so e.g. the daily chart still shows every day
  (so you can pick a different one) while narrowing to the selected
  facility/region; the selected bar/point is highlighted and the others
  dim rather than disappearing, so the rest of the data stays visible for
  comparison.

## Defense sheet — questions to be ready for

- **Why did you rebuild instead of continuing the group project's repo?**
  See "Origin and rebuild scope" above — new ownership, dead Azure
  credits, and a rebuild was the more honest way to demonstrate solo work.
- **Walk me through what you found wrong with the original code.** Same
  section — no real database, hardcoded credentials, the Update/Delete
  bug, the Singleton/Scoped mismatch, the direct `HttpClient` instantiation.
- **Why Postgres/Render instead of what the original used?** See "Why
  Postgres instead of Azure SQL" and "Why Render instead of Azure App
  Service" — cost and hosting-tier constraints, not a technology
  preference.
- **Why is the AI feature so limited in scope?** See Step 5's "Why this
  stays this narrow" — the app's core value is reliable data
  ingestion/storage/visualization; an LLM doesn't fix a problem that
  layer doesn't have. The one legitimate use is turning tabular data into
  a sentence a non-technical reader can skim.
- **Walk me through what the outage summary endpoint actually does, step
  by step.** Query filtered outage rows → aggregate per facility in
  C# → build a compact prompt (not raw rows) → one bounded Groq API call
  with an explicit "don't invent facts" system prompt → return the text.
  See Step 5.
- **Why Groq instead of OpenAI?** See the "OpenAI quota error, pivoted to
  Groq" entry in Challenges — hit a real billing wall, and the fix
  doubles as a decent story about provider-specific code being isolated
  in one service.
- **What does MCP actually solve that a REST API doesn't?** See Step 6's
  "What MCP actually is" — structured, self-describing tool discovery for
  AI clients, not a replacement for the REST API underneath it (the MCP
  server calls the REST API, it doesn't reimplement data access).
- **What would you need to add if this had to run in production at real
  utility scale?** Rate limiting/auth on the write endpoints (currently
  anonymous, same as the original MVC actions — noted as a known gap in
  Step 4), a queue or retry policy in front of the EIA ingestion job
  instead of an in-process `BackgroundService`, structured logging
  shipped somewhere durable instead of container stdout, and likely
  splitting the MCP server and API into separately deployable/scalable
  processes rather than the API being a single container.
- **How do you handle EIA API downtime or rate limits?** `EiaIngestionService`
  catches request exceptions and non-success responses, logs a warning,
  and returns `0` inserted rather than crashing the app or the background
  service loop — see Step 2 and the ingestion-service code itself.
- **What was the biggest change between your original version and this
  one, and why?** Arguably the roadmap pivot itself, documented at the top
  of the Roadmap section — this project changed identity partway through,
  from "keep adding generic full-stack features" to "REST API + one
  justified AI feature + MCP," to match the specific engineering roles
  this portfolio targets rather than breadth for its own sake.
