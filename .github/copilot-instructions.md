# Copilot Project Instructions

Concise operational guidance for AI assistants contributing to this repository. Focus on THESE concrete patterns; avoid inventing new abstractions unless asked.

## TODO Management
All todos should be saved to `todo.md` in the project root. Completed items must be clearly marked as completed in the file.

## Purpose
Automate comparison between GBFS bike share station data and OpenStreetMap (OSM); emit structured diff GeoJSON + optional Maproulette challenges + OSM rename changeset.

## Architecture & DI Pattern
- Main orchestration in `BikeShareFlows` class (injected dependencies)
- Services abstracted via interfaces in `Services/Interfaces.cs`
- Implementations in `Services/ServiceImplementations.cs`
- DI container configured in `Program.cs` with `Microsoft.Extensions.DependencyInjection`
- Always inject interfaces, never concrete classes when adding new services

## Core Flow (orchestrated in `BikeShareFlows`)
1. Load system from `src/bikeshare_systems.json` via `IBikeShareSystemLoader`
2. Ensure system scaffolding via `ISystemSetupService` – creates `data_results/<SYSTEM>/instructions/*.md` + `stations.overpass` if missing
3. Fetch GBFS stations via `IBikeShareDataFetcher.FetchStationsAsync()`
4. Write baseline `bikeshare.geojson` (record‑separated FeatureCollections; each line starts with RS `\u001e`) via `IGeoJsonWriter`
5. Git diff previous committed version via `IGitReader` → classify added/removed/moved/renamed via `IComparerService` (threshold: 3m internal diff, 30m vs OSM)
6. Generate diff files + OSM comparison via `IOSMDataFetcher` + `IGeoJsonWriter`
7. Optional Maproulette tasks via `IMaprouletteService` after `IPromptService` confirmation

## Critical File Format: Record-Separated GeoJSON
**NEVER modify this format without coordinated migration:**
```csharp
// Each line = \u001e + complete FeatureCollection JSON
var template = "\u001e{{\"type\":\"FeatureCollection\"" +
    ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
    "\"coordinates\":[{lat},{lon}]}},\"properties\":{{" +
    "\"address\":\"{id}\",\"name\":\"{name}\"...
```
- Parsing: `GeoPoint.ParseLine()` strips `\u001e` prefix
- Generation: `GeoJsonGenerator.GenerateGeojsonLine()` adds `\u001e` prefix
- Git-friendly: enables clean line-by-line diffs

## File Paths & IO Patterns
- All file operations through `FileManager` static class
- **Dynamic base path resolution**: `GetBasePath()` handles different working directories automatically
- System files under `data_results/<SYSTEM>/`
- Helper methods: `FileManager.GetSystemFilePath()`, `FileManager.WriteSystemGeoJsonFileAsync()`
- **Always use relative paths via FileManager helpers**
- Works from any directory: project root, `src/`, or build output directory

## Coordinate & Culture Handling
- **CRITICAL**: Always format lat/lon with `InvariantCulture` (prevents locale decimal separator issues)
- Coordinates rounded to 5 decimals in `GeoPoint.ParseCoords()`
- Distance calculations use Haversine formula in `BikeShareComparer.GetDistanceInMeters()`

## Comparison Logic & Thresholds
- Internal diff (GBFS vs previous): 3m threshold for "moved"
- OSM comparison: 30m threshold for "moved"
- Classification priority: moved > renamed (station can't be both)
- Station matching by stable `id` (GBFS `station_id` or OSM `ref`; fallback `osm_` prefix)

## Logging Patterns (Serilog)
- Structured logging: `Log.Information("Message {Property}", value)`
- Console + rolling file (7 days retention)
- Include context in service methods: system name, counts, file paths
- Error handling: log & continue for non-fatal operations (e.g., Overpass failures)

## CLI Commands & Development
```powershell
# Build & test
dotnet build
dotnet test

# Primary operations (works from any directory)
dotnet run -- 1                    # Run system ID 1 (from src/)
dotnet run --project src -- 1      # Run system ID 1 (from root)
dotnet run -- list                 # List systems
dotnet run -- validate 1           # Validate system setup
dotnet run -- fetch-brand-tags     # NSI brand tagging

# Project structure
src/                               # Main application
├── bikeshare_systems.json        # System configurations
├── BikeShareFlows.cs             # Main orchestration
├── Services/Interfaces.cs        # Service contracts
└── data_results/<SYSTEM>/        # Generated outputs

tests/                            # Unit tests (xUnit)
├── *Tests.cs                    # Test classes
└── data_results/                # Test fixtures
```

## Testing Patterns
- xUnit framework with `dotnet test`
- Test categories: Core logic → IO boundaries with mocks → Integration
- Mock external dependencies (HTTP, file system) using service interfaces
- Test data in `tests/data_results/` subdirectories
- Focus on deterministic logic first: `BikeShareComparer`, `GeoPoint.ParseLine()`, coordinate rounding

## External Dependencies & Environment
- **GBFS APIs**: `station_information.json` endpoints per system
- **Overpass API**: `https://overpass-api.de/api/interpreter` (system-specific queries in `stations.overpass`)
- **Maproulette API**: Requires `$env:MAPROULETTE_API_KEY` environment variable
- **Name Suggestion Index**: OSM brand tagging via GitHub raw URLs
- Git repository: Required for diff generation (`IGitReader.GetLastCommittedVersion()`)

## System Configuration (`bikeshare_systems.json`)
```json
{
  "id": 1,                              // Unique integer
  "name": "System Name",                // Display name
  "city": "City Name",                  // Location
  "gbfs_api": "https://...",           // GBFS endpoint (required)
  "maproulette_project_id": 12345,     // Numeric project ID
  "brand:wikidata": "Q123456"          // Optional: for NSI tagging
}
```

## Safe Extension Patterns
- New diff categories: Extend `IComparerService.Compare()` return tuple → add `IGeoJsonWriter` method → integrate in `BikeShareFlows`
- New file outputs: Add methods to `IGeoJsonWriter` interface, implement in service
- New external integrations: Create interface, implement service, register in DI container
- **Preserve existing file names/formats** unless coordinating consumer updates

## Common Pitfalls
- Missing `bikeshare_systems.json` → detailed loader guidance
- Locale decimal separators → always use `InvariantCulture`
- Record separator format changes → breaks git diff & parsing
- New system without git history → all stations treated as "added" (expected)
- Missing instruction templates → Maproulette validation fails
- Overpass rate limits → logged but non-fatal (don't abort main flow)

## When Unsure
- Follow existing service interface patterns for new functionality
- Check test coverage before modifying core comparison logic
- Preserve record-separated GeoJSON format unless explicitly changing it
- Ask for clarification on file format changes or new external integrations
