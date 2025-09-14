# Copilot Project Instructions

Concise operational guidance for AI assistants contributing to this repository. Focus on THESE concrete patterns; avoid inventing new abstractions unless asked.

## TODO Management
All todos should be saved to `todo.md` in the project root. Completed items must be clearly marked as completed in the file.

## Purpose
Automate comparison between GBFS bike share station data and OpenStreetMap (OSM); emit structured diff GeoJSON + optional Maproulette challenges + OSM rename changeset.

## Core Flow (orchestrated in `BikeShareFlows`)
1. Load system (`BikeShareSystemLoader`) from `bikeshare_systems.json` (root file; validate IDs & URLs)
2. Ensure per‑system scaffolding (`SystemSetupHelper.EnsureSystemSetUpAsync`) – creates `data_results/<SYSTEM>/instructions/*.md` + `stations.overpass` if missing
3. Fetch current GBFS stations (`BikeShareDataFetcher.FetchFromApiAsync`)
4. Write baseline `bikeshare.geojson` (record‑separated FeatureCollections; each line starts with RS `\u001e`) via `GeoJsonGenerator`
5. Git diff previous committed version (`GitDiffToGeojson.GetLastCommittedVersion`) → classify added/removed/moved/renamed (`BikeShareComparer`) threshold: 3m internal diff, 30m vs OSM
6. Generate diff files + OSM comparison (`OSMDataFetcher.FetchFromOverpassApiAsync` + `GeoJsonGenerator.GenerateOSMComparisonFilesAsync`)
7. Optional Maproulette tasks (`MaprouletteTaskCreator.CreateTasksAsync`) after interactive prompt; skips removed if new system; renames handled via changeset `.osc` file.

## Generated Files (all under `data_results/<SYSTEM>/`)
- Core: `bikeshare.geojson`
- Diff: `bikeshare_added.geojson`, `bikeshare_removed.geojson`, `bikeshare_moved.geojson`, `bikeshare_renamed.geojson`, `bikeshare_toreview.geojson`
- OSM compare: `bikeshare_missing_in_osm.geojson`, `bikeshare_extra_in_osm.geojson`, `bikeshare_moved_in_osm.geojson`, `bikeshare_renamed_in_osm.geojson`, `bikeshare_osm.geojson`
- Overpass query: `stations.overpass` (customizable; auto-created)
- OSM rename changes: `bikeshare_renames.osc`
- Task templates: `instructions/{added,removed,moved,renamed}.md`
- Brand tags: `brand_tags.osm` (OSM XML template with tagging suggestions from Name Suggestion Index)

## GeoJSON Line Format
Each line = whole FeatureCollection (single Feature) prefixed with `\u001e` for git-friendly diffs. Parsing uses `GeoPoint.ParseLine`. Maintain this exact structure when creating new generators.

## Key Conventions
- File IO centralized in `FileManager` (always use relative paths via helpers; base path is `..\\..\\..\\`).
- Do NOT change record‑separated format or RS prefix without coordinated migration.
- Comparisons rely on stable `id` (GBFS `station_id` or OSM `ref`; fallback prefixed with `osm_`).
- Distances (Haversine) in `BikeShareComparer.GetDistanceInMeters`; keep thresholds parametric if extending.
- Logging: Serilog configured in `Program.cs` (console + rolling file). Prefer structured logs (`Log.Information("Msg {Prop}", value)`).
- Overpass: Always read system-specific `stations.overpass` first; only generate default if missing.
- Maproulette task creation: one challenge per change type; tasks are individual RS-separated JSON station payloads. Renames intentionally excluded (bulk changeset instead).
- Interactive confirmation for task creation currently uses `Console.ReadKey()`; automated flows may need an injectable prompt layer (note before refactor).

## External Dependencies
- GBFS station_information endpoint per system (`gbfs_api` field).
- Overpass API (`https://overpass-api.de/api/interpreter`).
- Maproulette API (`https://maproulette.org/api/v2/*`) requires `MAPROULETTE_API_KEY` env var.
- Name Suggestion Index (`https://github.com/osmlab/name-suggestion-index/raw/main/data/brands/amenity/bicycle_rental.json`) for OSM brand tagging.
- AngleSharp only used by legacy HTML scraper (`FetchFromWebsiteAsync`) – avoid extending unless reviving that path.

## Adding / Modifying Systems
- Edit `bikeshare_systems.json`; keep unique integer `id`; validate URL.
- Each system entry supports these fields:
  - `id` (required): Unique integer identifier
  - `name` (required): Display name of the bike share system
  - `city` (required): City or region name
  - `gbfs_api` (required): GBFS station_information endpoint URL
  - `maproulette_project_id` (required): Numeric Maproulette project ID for task creation
  - `brand:wikidata` (optional): Wikidata Q-identifier for the bike share brand (e.g., "Q17018523" for Bike Share Toronto)
- First run auto-creates directory + instruction templates + `stations.overpass`.
- Use `fetch-brand-tags` command to download OSM tagging suggestions based on `brand:wikidata` values.
- Commit generated baseline GeoJSON so future diffs work (git history essential for `GetLastCommittedVersion`).

## Safe Change Guidelines
- When adding new diff categories, mirror existing pattern: compute in `BikeShareComparer` (extend return tuple cautiously) → add generate method in `GeoJsonGenerator` → integrate call site in `BikeShareFlows`.
- Preserve existing file names unless consumer tooling updated.
- For performance-sensitive batch additions (e.g., Maproulette tasks), consider future bulk upload but keep current per-task loop semantics until tested.

## Common Pitfalls
- Missing `bikeshare_systems.json` → loader throws detailed guidance.
- New system with no git history: treat all stations as added (handled; don't special-case unless changing baseline logic).
- Empty or absent instruction markdown blocks Maproulette creation (validation throws).
- Overpass rate limits: failures are logged & non-fatal; do not abort main GBFS diff generation.
- Locale issues: always format lat/lon with `InvariantCulture` (follow existing code).

## Extension Ideas (Only if Requested)
Caching Overpass responses, bulk Maproulette adds, status feed integration, CLI options instead of interactive prompt. Do not implement proactively.

## Quick Dev Commands
```powershell
# Build
 dotnet build
# List systems
 dotnet run -- list
# Run system id 1
 dotnet run -- 1
# Validate system 1
 dotnet run -- validate 1
# Fetch brand tags for all systems
 dotnet run -- fetch-brand-tags
# Fetch brand tags for specific system
 dotnet run -- fetch-brand-tags 1
# Download global GBFS service list
 dotnet run -- save-global-service
```

## When Unsure
Prefer reading existing generator/comparer patterns before introducing new serialization or diff logic. Ask for clarification if a change impacts file formats, diff semantics, or Maproulette workflow.
