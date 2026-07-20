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
data pipeline project](#). SQL Server itself can run free in Docker
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
        │   └── HomeController.cs
        ├── DataAccess/
        │   └── ApplicationDbContext.cs
        ├── Models/
        │   ├── OutageRecord.cs       # real EF Core entity
        │   ├── EiaOutageDto.cs        # raw EIA API response shape
        │   ├── ChartDataViewModel.cs
        │   └── FacilityRegionMap.cs
        ├── Services/
        │   └── OutageService.cs      # DB-backed, replaces the in-memory Singleton
        ├── Views/
        └── wwwroot/
```

## Roadmap

1. ✅ **Foundation** — real persistence, secrets cleanup, bug fixes, Docker (this step)
2. Scheduled background ingestion (`BackgroundService`) instead of fetch-on-page-load
3. JWT authentication, user accounts, watchlists
4. Search/filter improvements on the outage list
5. GitHub Actions CI (build + test on push; the original had a working
   build pipeline, just pointed at a now-dead Azure deploy target)
6. React frontend, replacing Razor views
7. Full Docker Compose for local dev (app + Postgres + any added services)
8. Live deployment to Render

## Challenges encountered (and how they were resolved)

- **The "database" wasn't real.** Confirmed by actually reading
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
