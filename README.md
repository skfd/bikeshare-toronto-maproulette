# Bike Share ↔ OSM Sync Helper

Minimal operator guide. For full technical documentation see `DEV_README.md`.

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
dotnet run -- list        # show systems
dotnet run -- 1           # run system with id 1
```

## Typical Run Flow
1. Tool fetches GBFS stations
2. Writes `data_results/<SYSTEM>/bikeshare.geojson`
3. Diffs against last committed version (if any)
4. Downloads OSM stations using `stations.overpass` (auto-created first run)
5. Writes comparison + diff files
6. Prompts you: create Maproulette tasks? (y/N)
7. If accepted & `MAPROULETTE_API_KEY` set + project id configured → challenges created
8. Separately apply rename changes using generated `bikeshare_renames.osc` in JOSM

## Required To Create Tasks
Set environment variable before running:
```powershell
$env:MAPROULETTE_API_KEY = "<your key>"
```
Ensure system entry has `"maproulette_project_id": <id>`.

## Operator Responsibilities
- Review GeoJSON outputs (`data_results/<SYSTEM>/`)
- Open diffs in JOSM / QGIS for validation
- **Check for `bikeshare_osm_duplicates.geojson`** - if present, fix duplicate ref values in OSM
- Complete Maproulette tasks (added / etc.)
- Load `bikeshare_renames.osc` in JOSM and upload after verifying on imagery / ground truth
- Commit updated `bikeshare.geojson` so next run has a baseline

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
instructions/*.md                     # MapRoulette task templates
```
All GeoJSON lines are record‑separated with an initial `\u001e` control character. Keep this format.

## Adding A System (Summary)
1. Add entry to `bikeshare_systems.json`
2. Run `dotnet run -- <id>` (scaffolds folders + templates)
3. Adjust `stations.overpass` if needed
4. Commit generated files

Full details: see `SETUP_NEW_SYSTEM.md` or `DEV_README.md`.

## Need More Detail?
See: `DEV_README.md` for architecture, development, logging, troubleshooting.