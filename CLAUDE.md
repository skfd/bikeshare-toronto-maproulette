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
│       ├── bikeshare_missing_in_osm.geojson  # Stations not in OSM
│       ├── bikeshare_extra_in_osm.geojson    # OSM stations not in GBFS
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

# Run the application (using PowerShell script - recommended)
.\sync.ps1                   # Sync all systems
.\sync.ps1 <system-id>       # Sync specific system
.\sync.ps1 -SkipTests        # Skip tests before syncing

# Or use dotnet directly
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
- `OVERPASS_API_URL`: Optional, defaults to public Overpass API

## Configuration Files
- `bikeshare_systems.json`: System configurations with GBFS URLs and MapRoulette project IDs
- `stations.overpass`: Per-system Overpass queries (auto-generated on first run)
- `instructions/*.md`: MapRoulette task templates (customizable per system)

## Data Flow
1. System configuration loaded from `bikeshare_systems.json`
2. GBFS data fetched and saved to `data_results/<SYSTEM>/bikeshare.geojson`
3. Git diff computed against last committed version
4. OSM data fetched using system-specific Overpass query
5. **OSM data validated** for duplicate ref values (generates `bikeshare_osm_duplicates.geojson` if issues found)
6. Comparison generates multiple GeoJSON outputs (diff and OSM comparison files)
7. Optional MapRoulette challenge creation
8. OSC file generated for bulk renames

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