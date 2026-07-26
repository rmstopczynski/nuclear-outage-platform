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
own, same discipline as the other project.

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
        │       └── TrendResponseDto.cs
        ├── Services/
        │   ├── OutageService.cs      # DB-backed, replaces the in-memory Singleton
        │   ├── EiaIngestionService.cs, EiaIngestionBackgroundService.cs
        │   ├── AuthService.cs, PasswordHasher.cs
        │   └── WatchlistService.cs
        ├── Views/
        └── wwwroot/
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
   aggregation, proper DTOs and pagination (this step)
5. Small AI-assisted outage summary (bounded OpenAI API call over
   structured data, not a chat feature)
6. (Optional) MCP server exposing outage data as a couple of tools, only
   if it's a genuine, demoable integration
7. Polish + documentation — fresh README pass, screenshots, defense notes

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
