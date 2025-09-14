# Developer Guide (Full Documentation)

This file preserves the original comprehensive README contents for contributors and maintainers. The primary `README.md` is now a concise operator-focused usage guide.

---

# Bike Share Location Comparison & Maproulette Task Generator

Automate synchronization between official bike share (GBFS) station data and what is mapped in OpenStreetMap (OSM). This tool:

* Downloads current station data (GBFS `station_information.json`)
* Compares against the last committed snapshot and OSM data (via custom Overpass queries)
* Produces rich GeoJSON diff + review files
* (Optionally) creates Maproulette challenges for community mapping
* Creates OSM changeset file for renamed stations (use JOSM to apply)

> Goal: Keep bike share station networks accurately mapped, highlight missing/moved/renamed infrastructure, and streamline community contribution workflows.

---

## Table of Contents
1. [Features](#features)
2. [Architecture Overview](#architecture-overview)
3. [Prerequisites](#prerequisites)
4. [Installation](#installation)
5. [Configuration](#configuration)
6. [CLI Usage](#cli-usage)
7. [Generated Outputs](#generated-outputs)
8. [Overpass Queries](#overpass-queries)
9. [Logging](#logging)
10. [Adding a New System](#adding-a-new-system)
11. [Maproulette Integration](#maproulette-integration)
12. [Troubleshooting](#troubleshooting)
13. [Development](#development)
14. [Roadmap / Ideas](#roadmap--ideas)
15. [Contributing](#contributing)
16. [License](#license)

---

## Features

### Core
* Multi‑system support (configure any GBFS network)
* Automatic first‑run setup (directories, instruction templates, default Overpass query)
* Git-aware diffing (compares with last committed GeoJSON state)
* Structured JSON & GeoJSON outputs for downstream tooling
* Deterministic station comparison (added / removed / moved / renamed)

### OSM / Overpass
* System‑scoped `stations.overpass` files (version controlled)
* Multi-area & complex query support
* Fail-safe behavior for new systems (no destructive assumptions)

### Maproulette (Optional)
* Project validation before task creation
* Challenge/task generation for new, moved, renamed stations
* Instruction templates per change type (editable Markdown)

### Developer Experience
* Modern CLI with `System.CommandLine`
* Structured logging via Serilog (console + rolling file sink)
* Clear exception surfaces with actionable messages

---

## Architecture Overview

| Component | Responsibility |
|-----------|----------------|
| `Program.cs` | CLI definition + logging bootstrap |
| `BikeShareFlows.cs` | Orchestrated workflows (run, validate, test project) |
| `BikeShareDataFetcher.cs` | GBFS API ingestion |
| `OSMDataFetcher.cs` | Overpass fetch + caching of OSM station data |
| `GeoJsonGenerator.cs` | GeoJSON & diff file creation |
| `BikeShareComparer.cs` | Spatial + attribute comparison logic |
| `GitDiffToGeojson.cs` / `GitFunctions.cs` | Git history access for previous snapshot |
| `MaprouletteTaskCreator.cs` | Challenge/task creation & validation |
| `SystemSetupHelper.cs` | First-run + instruction file scaffolding |
| `OsmFileFunctions.cs` | Helper for rename change file generation |

All outputs live under `data_results/<SYSTEM_NAME>/`.

---

## Prerequisites
* .NET 8 SDK
* Git (for diff & last commit date detection)
* Maproulette API key (optional, for task creation)
* Overpass API reachable (default public endpoint used)

---

## Installation
```bash
git clone https://github.com/<your-org-or-user>/bikeshare-toronto-maproulette.git
cd bikeshare-toronto-maproulette
dotnet build
```

Run help:
```bash
dotnet run -- --help
```

---

## Configuration

Systems are defined in `bikeshare_systems.json`:
```jsonc
[
  {
    "id": 1,
    "name": "Bike Share Toronto",
    "city": "Toronto",
    "maproulette_project_id": 60735,
    "gbfs_api": "https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information.json"
  }
]
```

Field notes:
| Field | Description |
|-------|-------------|
| `id` | Unique integer used in CLI commands |
| `name` | System name (directory identifier) |
| `city` | Used to generate default Overpass query |
| `maproulette_project_id` | >0 enables task creation; use -1 or omit to disable |
| `gbfs_api` | GBFS station_information endpoint URL |

Environment variable (optional):
| Variable | Purpose |
|----------|---------|
| `MAPROULETTE_API_KEY` | Required for creating Maproulette challenges/tasks |

See full guide: [SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md)

---

## CLI Usage

Core pattern:
```bash
dotnet run -- <command> [options]
```

You may also pass a system id directly to the root (equivalent to `run`).

### Commands
| Command | Description | Example |
|---------|-------------|---------|
| (root) | Run comparison for system | `dotnet run -- 1` |

New scaffolding-only command:

- `dotnet run -- setup <id>` Creates instruction templates + stations.overpass for the system, then exits without fetching GBFS/OSM data. First invocation of a new system via `run` now also auto-scaffolds and exits early.
| `run <system-id>` | Same as root run | `dotnet run -- run 2` |
| `list` | List configured systems | `dotnet run -- list` |
| `validate <system-id>` | Validate system config + instruction files + project | `dotnet run -- validate 1` |
| `test-project <project-id>` | Check Maproulette project accessibility | `dotnet run -- test-project 60735` |

### Typical Workflow
```bash
# 1. Inspect systems
dotnet run -- list

# 2. Run a system comparison (creates/updates data_results/<NAME>)
dotnet run -- 1

# 3. (Optional) Accept prompt to create Maproulette tasks

# 4. Review GeoJSON outputs & open in JOSM / QGIS
```

---

## Generated Outputs
All under: `data_results/<SYSTEM_NAME>/`

| File | Purpose |
|------|---------|
| `bikeshare.geojson` | Current fetched station set |
| `bikeshare_added.geojson` | Newly added stations since last commit |
| `bikeshare_removed.geojson` | Stations missing vs previous snapshot |
| `bikeshare_moved.geojson` | Stations moved beyond threshold (coordinates changed) |
| `bikeshare_renamed.geojson` | Name changed but same location |
| `bikeshare_missing_in_osm.geojson` | Present in API, absent in OSM |
| `bikeshare_extra_in_osm.geojson` | Present in OSM, absent in API |
| `bikeshare_moved_in_osm.geojson` | Spatial divergence vs OSM data |
| `bikeshare_renamed_in_osm.geojson` | OSM name mismatch |
| `bikeshare_renames.osc` | OSM changeset file for batch rename edits |
| `stations.overpass` | Query file used to fetch OSM data |
| `instructions/*.md` | Maproulette challenge/task templates |

Diff GeoJSON lines are emitted as record‑separated objects (RS `\u001e`) for efficient streaming & git diff friendliness.

---

## Overpass Queries

Each system owns a query file: `data_results/<SYSTEM_NAME>/stations.overpass`.

Auto‑generated default (single city):
```overpass
[out:json];
area[name="City Name"]->.city;
(
  node(area.city)[bicycle_rental=docking_station];
  way(area.city)[bicycle_rental=docking_station];
  relation(area.city)[bicycle_rental=docking_station];
);
out meta;
```

Customize for multi‑region, operator filtering, or advanced spatial scopes. Test queries at https://overpass-turbo.eu/ before rerunning.

---

## Logging

Powered by Serilog.

| Sink | Location / Format |
|------|-------------------|
| Console | `[HH:mm:ss LVL] Message` minimal formatting |
| File (rolling) | `logs/bikeshare-YYYYMMDD.log` (7 days retained) |

Structured properties are embedded for later analysis (JSON in file sink). Adjust levels in `Program.cs` if needed.

Example snippet:
```
[12:04:11 INF] Starting comparison run for Bike Share Toronto (Toronto) Id=1 Project=60735
[12:04:12 INF] Generating diff files for Bike Share Toronto: Added=2 Removed=0 Moved=1 Renamed=0
```

---

## Adding a New System
1. Append entry to `bikeshare_systems.json`
2. (Optional) Set `maproulette_project_id` (>0) for task creation
3. Run:
   ```bash
   dotnet run -- <new-id>
   ```
4. Inspect generated `data_results/<NAME>/stations.overpass` and adjust if needed
5. Commit generated files (provides future diff baseline)

Full guide: [SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md)

---

## Maproulette Integration
When a project ID is configured and the API key is present:
* Project is validated before any challenge work
* Tasks are generated for new / moved / renamed stations (removed stations are treated conservatively for new systems)
* Instruction Markdown templates are substituted with station fields (e.g., `{{name}}`, `{{capacity}}`)

You will be prompted interactively after a run to confirm task creation. Decline to skip.

Environment:
```bash
export MAPROULETTE_API_KEY=xxxxxxxxxxxxxxxx
```
On Windows PowerShell:
```powershell
$env:MAPROULETTE_API_KEY = "xxxxxxxx"
```

---

## Troubleshooting
| Symptom | Action |
|---------|--------|
| No systems listed | Check JSON syntax / run `dotnet run -- list` |
| Empty diff files | No changes vs last committed snapshot |
| OSM comparison empty | Overpass query might be too restrictive; inspect `stations.overpass` |
| Task creation failed | Verify `MAPROULETTE_API_KEY` and project permissions |
| GBFS fetch error | Open URL in browser, confirm valid JSON + CORS not blocking |

Verbose diagnostics are in `logs/`.

---

## Development
Build:
```bash
dotnet build
```

Run all commands from repo root. Recommend committing generated GeoJSON periodically to maintain accurate diff baselines.

Code layout favors explicit orchestrator flows (`BikeShareFlows`) with pure helpers for testability.

---

## Roadmap / Ideas
* Optional Spectre.Console formatting layer
* Automated Overpass rate limiting / retry
* Support for station status feed (dock counts)
* Publish as a .NET tool (`dotnet tool install ...`)
* CSV export / summary stats
* Web dashboard overlay (leaflet) for diffs

Feel free to open an issue to discuss or add more.

---

## Contributing
1. Fork & branch
2. Add / adjust a system or feature
3. Run formatting & build
4. Provide concise PR with context + screenshots (if UI/log output relevant)

Small improvements welcome—documentation, Overpass query examples, and diff heuristics.

---

## License
TBD (Add a LICENSE file if distributing publicly). Until then, assume personal / experimental use only.

---

## Resources
* GBFS Spec: https://github.com/NABSA/gbfs
* OSM Bike Share Tagging: https://wiki.openstreetmap.org/wiki/Tag:amenity%3Dbicycle_rental
* Maproulette Docs: https://github.com/maproulette/maproulette-backend/tree/main/docs
* Overpass Turbo: https://overpass-turbo.eu/

---

> If you use this for another city/network, please share improvements back!

