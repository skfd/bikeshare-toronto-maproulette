# Bike Share ↔ OSM Sync Helper

Minimal operator guide. For full technical documentation see `DEV_README.md`.

## Intended Use

**This is a local workstation tool for trusted operators.**

- Runs on your personal computer, not a server
- You control all inputs (config files, command-line arguments)
- Fetches data from trusted public APIs (GBFS feeds, Overpass API)
- No web interface, no remote access, no multi-user scenarios
- System names come from `bikeshare_systems.json` (which you maintain)

**Trust model:** The tool trusts that you're not trying to attack yourself. Basic filesystem safety prevents accidental typos, not malicious input.

## What It Does
Fetches official GBFS station data, compares it with:
1. Previous committed snapshot
2. Current OpenStreetMap data (via Overpass)

Then produces:
- Diff GeoJSON (added / removed / moved / renamed)
- OSM comparison GeoJSON (missing / extra / moved / renamed)
- OSM validation reports (duplicate ref values)
- Optional Maproulette challenges (added + optionally others)
- `.osc` file for batch station renames (apply in JOSM)

## Quick Start
```bash
git clone https://github.com/<your-org-or-user>/bikeshare-toronto-maproulette.git
cd bikeshare-toronto-maproulette
dotnet build
```
Add or edit `bikeshare_systems.json` (example already present). Then run:
```bash
dotnet run -- list          # show systems
dotnet run -- 1             # run system with id 1
dotnet run -- 1,3,5         # run several
dotnet run -- all           # run every configured system
dotnet run -- 1 -v          # verbose output
dotnet run -- 1 -q          # quiet output
```

Flags for unattended runs:

| Flag | Effect |
|---|---|
| `--yes` / `-y` | Answer every confirmation with yes. Separate from `--quiet` on purpose: quiet changes what you see, `--yes` changes what the tool does. |
| `--commit-baseline` | Commit the refreshed GeoJSON as the next baseline when it changed. |

Every run writes `data_results/<SYSTEM>/last_run.md` and `last_run.json` — the
operator checklist, kept after the console scrolls away.

## Typical Run Flow
1. Tool fetches GBFS stations
2. Writes `data_results/<SYSTEM>/bikeshare.geojson`
3. Diffs against last committed version (if any)
4. Downloads OSM stations using `stations.overpass` (auto-created first run)
5. Writes comparison + diff files
6. Prompts you: sync MapRoulette challenges? (y/N)
7. If accepted & `MAPROULETTE_API_KEY` set + project id configured → challenges refreshed
8. Separately apply rename changes using generated `bikeshare_renames.osc` in JOSM

## Scheduled Operation

`.github/workflows/weekly-sync.yml` runs every system weekly (Wednesday 11:00 UTC),
refreshes the challenges, commits the new baseline and opens an issue with the
combined checklist. It needs the `MAPROULETTE_API_KEY` repository secret. Trigger
it by hand from the Actions tab, optionally unticking *Create and close MapRoulette
tasks* for a side-effect-free fetch-and-diff.

The JOSM half of the work stays manual — the issue tells you which `.osc` files
are waiting.

## Challenge Lifecycle

There is **one long-lived challenge per system per change type**, recorded in
`data_results/<SYSTEM>/maproulette_manifest.json` (committed). A run updates it in
place rather than creating a new one, which is what makes running on a schedule safe:

| Situation | What happens |
|---|---|
| Nobody works the tasks | They stay open. The next run adds only genuinely new stations — no duplicate challenge, no duplicate tasks. |
| Somebody fixes a few | Those stations are in OSM now and drop out of the comparison; their tasks are already marked Fixed. Nothing to do. |
| Fixed in OSM without touching MapRoulette | The next run sees the station in the OSM fetch and closes the task as *Already Fixed*. |
| Station disappears from GBFS | The task is closed as *Deleted* — it no longer describes anything real. |
| Mapper marked it *Not an Issue* / *Too Difficult* | Never touched. Only `created` and `skipped` tasks are ever modified. |

Two rules make this safe to run unattended:

- **Tasks are only closed on primary evidence** from that run's fetches — the
  station appearing in OSM, or leaving the GBFS feed. Never because it merely fell
  out of the comparison file, which a threshold change or a partial Overpass
  response can cause on its own.
- **A fetch that looks truncated aborts the run** before anything is closed. If
  the OSM query returns nothing, or under half the stations seen last time, the run
  fails loudly instead of closing every open task.

## Required To Create Tasks
Set `MAPROULETTE_API_KEY` before running. Pick whichever matches your shell:

```bash
# Git Bash / WSL / macOS / Linux (current shell only)
export MAPROULETTE_API_KEY="<your key>"

# PowerShell (current shell only)
$env:MAPROULETTE_API_KEY = "<your key>"

# PowerShell (persistent, user-level)
[Environment]::SetEnvironmentVariable("MAPROULETTE_API_KEY", "<your key>", "User")

# cmd.exe (current shell only)
set MAPROULETTE_API_KEY=<your key>

# cmd.exe (persistent, user-level)
setx MAPROULETTE_API_KEY "<your key>"
```

Verify it's set:
```bash
echo "$MAPROULETTE_API_KEY"          # bash
echo $env:MAPROULETTE_API_KEY        # PowerShell
echo %MAPROULETTE_API_KEY%           # cmd
```

Ensure system entry has `"maproulette_project_id": <id>`.

## Operator Responsibilities
- Review GeoJSON outputs (`data_results/<SYSTEM>/`)
- Open diffs in JOSM / QGIS for validation
- **Check for `bikeshare_osm_duplicates.geojson`** - if present, fix duplicate ref values in OSM
- Complete Maproulette tasks (added / etc.)
- Load `bikeshare_renames.osc` in JOSM and upload after verifying on imagery / ground truth
- Commit updated `bikeshare.geojson` so next run has a baseline (or pass `--commit-baseline`)

## MapRoulette Workflow

The per-task loop we use when resolving challenges. Keep JOSM open in the background with **Remote Control** enabled (*JOSM → Preferences → Remote Control → Enable remote control*) so MapRoulette can hand tasks off to it.

### The loop
Repeat for each task:

1. **In MapRoulette** — click the task's suggested tags to copy them to the clipboard, then press **`R`** to send the feature to JOSM.
2. **`Alt+Tab`** to JOSM.
3. Make the edit (move the node, paste tags, rename, etc.).
4. **`Ctrl+Shift+↑`** — open the upload dialog.
5. **`Ctrl+Enter`** — confirm the upload.
6. **`Alt+Tab`** back to MapRoulette.
7. **`F`** — mark the task as *Fixed*.
8. **`Shift+Tab`** — advance to the next task.

### Other MapRoulette completion keys
Use instead of `F` when the situation calls for it:

| Key | Status              | When to use                                  |
|-----|---------------------|----------------------------------------------|
| `F` | Fixed               | You made the edit in JOSM.                   |
| `X` | Already Fixed       | Someone already resolved it; no edit needed. |
| `Q` | Not an Issue        | The task is a false positive.                |
| `D` | Too Difficult       | Can't verify from imagery / ground truth.    |
| `W` | Skip                | Come back to it later.                       |

References: [MapRoulette keyboard shortcuts](https://learn.maproulette.org/en-US/documentation/using-keyboard-shortcuts/) · [JOSM shortcuts](https://josm.openstreetmap.de/wiki/Shortcuts).

## Key Files (per system)
```
bikeshare.geojson                     # Current GBFS data
bikeshare_added.geojson               # New stations (vs git)
bikeshare_removed.geojson             # Removed stations (vs git)
bikeshare_moved.geojson               # Moved stations (vs git)
bikeshare_renamed.geojson             # Renamed stations (vs git)
bikeshare_osm.geojson                 # Current OSM data
bikeshare_osm_duplicates.geojson      # OSM data quality issues (if found)
bikeshare_missing_in_osm.geojson      # Stations not in OSM
bikeshare_extra_in_osm.geojson        # OSM stations not in GBFS
bikeshare_renames.osc                 # JOSM changeset for renames
stations.overpass                     # Overpass query for OSM data
maproulette_manifest.json             # Which challenge holds which tasks (committed)
last_run.md / last_run.json           # The operator checklist from the last run
instructions/*.md                     # MapRoulette task templates
```
All GeoJSON lines are record‑separated with an initial `\u001e` control character. Keep this format.

## Temporarily Disused Stations

OSM stations tagged with `disused:amenity=bicycle_rental` are treated as **temporarily disused** and are automatically excluded from the "extra in OSM" removal list (`bikeshare_extra_in_osm.geojson`). This prevents creating MapRoulette tasks that would suggest removing stations that are only temporarily closed.

When disused stations are detected, the tool:
- Logs a warning listing each skipped station (ID, name, OSM type/ID)
- Displays a console warning with the count of skipped stations
- Excludes them from `bikeshare_extra_in_osm.geojson`

**Note for custom Overpass queries:** If you maintain a custom `stations.overpass` file, add queries for `disused:amenity=bicycle_rental` to ensure these stations are detected:
```
node(area.city)["disused:amenity"=bicycle_rental];
way(area.city)["disused:amenity"=bicycle_rental];
```
Newly generated default queries include these automatically.

## Adding A System (Summary)
1. Add entry to `bikeshare_systems.json`
2. Run `dotnet run -- <id>` (scaffolds folders + templates)
3. Adjust `stations.overpass` if needed
4. Commit generated files

Full details: see `SETUP_NEW_SYSTEM.md` or `DEV_README.md`.

## Configuration Options

Each system in `bikeshare_systems.json` supports optional configuration fields. For complete details, see **[CONFIGURATION.md](CONFIGURATION.md)**.

Key optional fields:
- **`move_threshold_meters`** (default: 3.0): Distance threshold for detecting moved stations
- **`osm_comparison_threshold_meters`** (default: 30.0): Distance threshold for OSM comparison
- **`station_name_prefix`**: Prefix to add to station names (e.g., `"Citi Bike - "`)
- **`brand:wikidata`**: Wikidata ID for the bike share brand
- **`maproulette_project_id`**: MapRoulette project ID for task creation

See `bikeshare_systems.example.json` for a complete configuration example.

## Need More Detail?
See: `DEV_README.md` for architecture, development, logging, troubleshooting.