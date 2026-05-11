# Bike Share ↔ OSM Sync Helper - AI Assistant Context

## Project Overview
This is a C#/.NET 8 application that synchronizes bike share station data from GBFS (General Bikeshare Feed Specification) providers with OpenStreetMap (OSM). It identifies discrepancies, generates diffs, and creates MapRoulette challenges for community mappers to resolve.

## Core Functionality
1. **GBFS Data Fetching**: Downloads current station information from bike share APIs
2. **OSM Comparison**: Queries Overpass API for existing OSM bike share stations
3. **OSM Data Validation**: Detects duplicate ref values and generates validation reports
4. **Diff Generation**: Creates GeoJSON files showing added/removed/moved/renamed stations
5. **MapRoulette Integration**: Optionally creates mapping tasks for community resolution
6. **OSM Changeset Generation**: Creates .osc files for batch station renames in JOSM

## Tech Stack & Dependencies
- **Language**: C# with .NET 8
- **CLI Framework**: System.CommandLine (beta)
- **Logging**: Serilog (with console and file sinks)
- **Testing**: NUnit with Moq for mocking
- **HTTP Parsing**: AngleSharp
- **DI Container**: Microsoft.Extensions.DependencyInjection
- **CI/CD**: Minimal GitHub Actions (build + test only)

## Project Structure
```
bikeshare-sync/
├── src/                           # Main application code
│   ├── Program.cs                # Entry point with CLI setup
│   ├── BikeShareFlows.cs         # Main workflow orchestration
│   ├── Services/                 # Service interfaces and implementations
│   ├── bikeshare_systems.json    # Configuration for bike share systems
│   └── prepareBikeParking.csproj # Main project file
├── tests/                         # Comprehensive test suite
│   └── prepareBikeParking.Tests.csproj
├── data_results/                  # Generated outputs (per system)
│   └── <SYSTEM_NAME>/
│       ├── bikeshare.geojson              # Current GBFS data
│       ├── bikeshare_added.geojson        # New stations (vs git)
│       ├── bikeshare_removed.geojson      # Removed stations (vs git)
│       ├── bikeshare_moved.geojson        # Moved stations (vs git)
│       ├── bikeshare_renamed.geojson      # Renamed stations (vs git)
│       ├── bikeshare_osm.geojson          # Current OSM data
│       ├── bikeshare_osm_duplicates.geojson  # OSM validation issues (if found)
│       ├── bikeshare_missing_in_osm.geojson  # Stations not in OSM (closed stations excluded)
│       ├── bikeshare_extra_in_osm.geojson    # OSM stations not in GBFS (ref-conflict nodes excluded)
│       ├── bikeshare_closed.geojson       # GBFS stations closed per station_status (for manual review)
│       ├── bikeshare_ref_conflicts.geojson    # OSM elements with a stale/recycled ref (if any)
│       ├── bikeshare_ref_conflicts.osc        # JOSM changeset adding the correct ref/ref:gbfs to ref-less nodes (if any)
│       ├── bikeshare_renames.osc          # JOSM changeset for renames
│       └── stations.overpass              # Overpass query for this system
└── logs/                          # Rolling log files

```

## Key Design Patterns
1. **Dependency Injection**: All services registered in DI container
2. **Service Layer Pattern**: Clear separation between interfaces and implementations
3. **Command Pattern**: CLI commands mapped to flow methods
4. **Repository Pattern**: Git operations abstracted through services

## Code Conventions
- **Naming**: PascalCase for public members, camelCase for private fields
- **Async**: All IO operations use async/await pattern
- **Nullable**: Nullable reference types enabled project-wide
- **Logging**: Structured logging with Serilog throughout
- **Error Handling**: Explicit try-catch with detailed logging
- **File Format**: UTF-8 encoding enforced for all text files
- **GeoJSON**: Uses record separator (U+001E) for line separation

## Build & Test Commands
```bash
# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run -- list           # List configured systems
dotnet run -- <system-id>    # Run for specific system
dotnet run -- <system-id> -v # Run with verbose output
dotnet run -- <system-id> -q # Run with quiet output
dotnet run -- test-project <system-id>  # Test MapRoulette connection

# Clean build artifacts
dotnet clean
```

## Environment Variables
- `MAPROULETTE_API_KEY`: Required for creating MapRoulette challenges
- `OVERPASS_API_URL`: Optional comma-separated list of Overpass endpoints in priority order. Defaults to overpass-api.de + public mirrors. Requests retry each endpoint with exponential backoff before failing over to the next.

## Configuration Files
- `bikeshare_systems.json`: System configurations with GBFS URLs, MapRoulette project IDs, and optional thresholds
  - `move_threshold_meters` (default: 3.0): Distance threshold for detecting moved stations in git diff
  - `osm_comparison_threshold_meters` (default: 30.0): Distance threshold for matching stations with OSM
  - `ref_conflict_threshold_meters` (default: `osm_comparison_threshold_meters` × 10, min 100.0): Distance beyond which an OSM element that shares a `ref` with a GBFS station is treated as a stale/recycled id rather than a moved station (see "Ref Conflict Handling" below)
  - `station_name_prefix`: Optional prefix to add to all station names
  - `expand_street_names` (default: false): When true, expand abbreviated street tokens (Ave→Avenue, St→Street, N→North, etc.) per [OSM convention](https://wiki.openstreetmap.org/wiki/Abbreviations). Splits intersection names on `&`; preserves a leading `St` (Saint) per side; only expands directions at start/end positions so middle initials like `S.` are left alone. Applied before any prefix.
  - `brand:wikidata`: Wikidata ID for the bike share brand
- `stations.overpass`: Per-system Overpass queries (auto-generated on first run)
- `instructions/*.md`: MapRoulette task templates (customizable per system)

## Data Flow
1. System configuration loaded from `bikeshare_systems.json`
2. GBFS data fetched and saved to `data_results/<SYSTEM>/bikeshare.geojson`
3. Git diff computed against last committed version
4. OSM data fetched using system-specific Overpass query
5. **OSM data validated** for duplicate ref values (generates `bikeshare_osm_duplicates.geojson` if issues found)
6. **Temporarily disused stations** (tagged `disused:amenity=bicycle_rental`) are detected and excluded from the "extra in OSM" removal list
7. **Closed GBFS stations** (per `station_status.json`) are detected and excluded from "missing in OSM" and from rename/move detection (see "Closed Station Handling" below)
8. **Ref conflicts** (OSM `ref` tag points at a station GBFS no longer uses that id for — usually a recycled id) are detected; ref-less OSM nodes that clearly are a current station get an auto-fix `.osc`, and the affected ids are dropped from the missing/moved lists (see "Ref Conflict Handling" below)
9. Comparison generates multiple GeoJSON outputs (diff and OSM comparison files)
10. Optional MapRoulette challenge creation
11. OSC files generated for bulk renames, reactivations, ref:gbfs additions, and ref conflicts

## Disused Station Handling
Stations tagged with `disused:amenity=bicycle_rental` in OSM are considered temporarily closed. These are:
- Parsed from Overpass results (whether they also have `bicycle_rental=docking_station` or not)
- Marked with `IsDisused = true` on the `GeoPoint` model
- **Excluded from `bikeshare_extra_in_osm.geojson`** to prevent creating removal tasks for temporarily closed stations
- Logged with a warning listing each skipped station's ID, name, and OSM element

Custom `stations.overpass` files should include `["disused:amenity"=bicycle_rental]` queries to detect these stations. Default generated queries include them automatically.

## Closed Station Handling (GBFS side)
The mirror of disused-OSM handling, on the GBFS feed. Each run also fetches `station_status.json` (URL derived from `gbfs_api` by swapping the last path segment to `station_status`, via `BikeShareSystem.GetStationStatusUrl()`). A station is "closed" when its status entry reports `is_installed=false` (decommissioned) or `is_installed=true` but `is_renting=false`/`is_returning=false` (temporarily out of service). These are:
- Marked with `IsClosed = true` on the `GeoPoint` model (set in `BikeShareDataFetcher`)
- **Excluded from `bikeshare_missing_in_osm.geojson`** so mappers aren't told to add closed stations
- **Excluded from rename/move detection** (`bikeshare_renamed_in_osm.geojson`, `bikeshare_moved_in_osm.geojson`, and the rename `.osc`) so no edits are auto-generated for stations the operator decommissioned
- **Written to `bikeshare_closed.geojson`** (the full set, including those that exist in OSM) for manual review — e.g. deciding whether an OSM node needs `disused:amenity=bicycle_rental`
- Logged with a warning listing each closed station's ID and name

Fetching/parsing `station_status.json` is a soft dependency: a 404, network error, malformed body, or empty `data.stations` is logged and the run proceeds with no stations flagged closed. Older feeds (PBSC v1 like Toronto/àVélo, legacy SoBi like Hamilton) hardcode the status flags to true, so this is a graceful no-op for them.

## Ref Conflict Handling
The GBFS↔OSM join key is `ref` (see memory: `ref:gbfs` is validation-only). When an operator **recycles a station id** — gives id `N` to a new station while OSM still has `ref=N` on the old one — the id-based comparison goes wrong: the new station looks like it "moved" (sometimes kilometres) and a separate OSM node carrying the correct *name* but no `ref` gets flagged as "extra in OSM" (a false deletion candidate). `RefConflictDetector` (run in `BikeShareFlows.CompareWithOSMData`) catches both shapes by cross-checking name + proximity:

- **`fix-ref`** — an OSM element with no `ref` whose normalized name exactly matches exactly one current GBFS station within `osm_comparison_threshold_meters`. We're confident what it is, so it's:
  - **removed from `bikeshare_extra_in_osm.geojson`** (no false deletion task)
  - **written to `bikeshare_ref_conflicts.osc`** as a `<modify>` adding `ref` and `ref:gbfs` (review + upload in JOSM, like the rename `.osc`)
  - its GBFS id removed from `bikeshare_missing_in_osm.geojson`
- **`review-ref`** — an OSM element that *does* carry a `ref`, but the GBFS station with that id is more than `ref_conflict_threshold_meters` away (a stale/recycled id). We don't auto-edit it (changing a `ref` risks creating a duplicate; the right id is often a chain the operator must untangle). It's surfaced in `bikeshare_ref_conflicts.geojson` (with a best-effort "likely ref" when its name matches a nearby station), and its id is **dropped from `bikeshare_moved_in_osm.geojson`** so the bogus "move" disappears.

Ambiguous cases (a no-ref node matching multiple stations, or multiple no-ref nodes matching one station) are logged and skipped — never auto-edited. Both `.osc` and `.geojson` outputs are removed when there are no conflicts, so a stale file is never left behind. Name matching is case/whitespace-insensitive and normalizes curly apostrophes, but is otherwise strict (no token-set fuzzing) to keep auto-generated edits safe; a `station_name_prefix` will simply suppress detection rather than risk a wrong match.

## Testing Strategy
- Unit tests for all core services
- Integration tests for API interactions
- Performance tests for large datasets
- Idempotency tests for Overpass queries
- Culture/formatting tests for internationalization
- Data validation tests (duplicate detection, path sanitization, etc.)

## Security & Trust Model

**This is a local workstation tool for trusted operators.**

### Design Assumptions
- Single user running on their own workstation
- Operator controls all configuration files
- No web interface, no remote access, no multi-user scenarios
- System names come from `bikeshare_systems.json` (operator-maintained)
- Data fetched from trusted public APIs (GBFS, Overpass)

### Actual Security Measures
- API keys stored as environment variables, never in code
- Basic path sanitization prevents accidental filesystem errors
- No credentials stored in version control
- HTTPS used for all API communications

### Not Applicable
This tool does NOT need protection against malicious input because:
- The operator is the only user (you can't attack yourself)
- All inputs are operator-controlled (config files, CLI args)
- No untrusted data processing (just public GBFS/OSM data)
- No multi-tenant or web service scenarios

## Common Tasks

### Add a New Bike Share System
1. Add entry to `src/bikeshare_systems.json`
2. Run `dotnet run -- <new-id>` to scaffold directories
3. Adjust generated `stations.overpass` query if needed
4. Test with `dotnet run -- test-project <new-id>`

### Debug API Issues
1. Check logs in `logs/` directory
2. Use `--verbose` flag for detailed output
3. Verify API endpoints are accessible
4. Check rate limiting and API keys

### Contribute Changes
1. Follow existing code patterns
2. Add unit tests for new functionality
3. Update this CLAUDE.md if adding major features
4. Ensure all tests pass before committing

## Performance Notes
- Overpass queries cached locally to reduce API load
- Git operations optimized for large GeoJSON files
- Parallel processing where applicable
- Memory-efficient streaming for large datasets

## Future Improvements
See `improvement-plan.md` for detailed enhancement roadmap.