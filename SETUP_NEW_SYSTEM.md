# Setting Up New Bike Share Systems

This guide explains how to add and configure new bike share systems for the Bike Share Location Comparison Tool.

## Quick Start

To add a new bike share system, you only need to:

1. **Add system configuration** to `bikeshare_systems.json`
2. **Run the tool** - everything else is created automatically!

```bash
# Add your system to bikeshare_systems.json, then:
dotnet run <your-system-id>
```

## Required Configuration

### Minimal bikeshare_systems.json Entry

Add your new system to the `bikeshare_systems.json` file:

```json
{
  "id": 3,
  "name": "Your System Name",
  "city": "Your City",
  "maproulette_project_id": -1,
  "gbfs_api": "https://your-api-endpoint.com/station_information.json"
}
```

### Field Descriptions

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `id` | ? Yes | Unique numeric identifier | `3` |
| `name` | ? Yes | System name (used for directories) | `"Capital Bikeshare"` |
| `city` | ? Yes | City name (used for OSM queries) | `"Washington DC"` |
| `maproulette_project_id` | ? Yes | Maproulette project ID (-1 if none) | `12345` or `-1` |
| `gbfs_api` | ? Yes | GBFS station_information endpoint | `"https://api.url/station_information.json"` |

## What Happens Automatically

When you run the tool for a new system, it automatically:

### 1. **Creates Directory Structure**
```
data_results/
??? Your System Name/
    ??? instructions/
        ??? added.md
        ??? removed.md
        ??? moved.md
        ??? renamed.md
```

### 2. **Generates Instruction Templates**
Creates Maproulette instruction files with system-specific OpenStreetMap tags:

**For `added.md`:**
```markdown
Add a point with these tags, or update existing point with them:

```
ref={{address}}
name={{name}}
capacity={{capacity}}
fixme=please set exact location
amenity=bicycle_rental
bicycle_rental=docking_station
brand=Your System Name
operator=Your System Name
operator:type=public
```
```

### 3. **Smart New System Detection**
The tool automatically detects new systems by checking git history for `bikeshare.geojson`. For new systems:

- ? **Prevents Deletion Tasks**: Skips creating "removed" tasks to avoid deleting existing OSM data
- ? **Focuses on Adding**: Creates only "added" and "moved" tasks for stations missing from OSM
- ? **Preserves OSM Data**: Protects existing bike share stations already mapped in OpenStreetMap

### 4. **System-Specific Processing**
- Fetches data from your GBFS API
- Creates system-specific GeoJSON files
- Handles git integration for change tracking
- Generates OSM comparison files
- **New System Behavior**: Treats all API stations as potentially new additions

### Task Creation Behavior

The tool creates different types of Maproulette tasks based on the system status:

#### **For Established Systems** (with git history):
- ? **Added Tasks**: New stations since last sync
- ? **Removed Tasks**: Stations deleted since last sync  
- ? **Moved Tasks**: Stations relocated since last sync
- ?? **Renamed Tasks**: Handled via bulk changeset (not individual tasks)

#### **For New Systems** (no git history):
- ? **Added Tasks**: Stations missing from OpenStreetMap
- ? **Removed Tasks**: **SKIPPED** to protect existing OSM data
- ? **Moved Tasks**: Stations with different coordinates in OSM
- ?? **Renamed Tasks**: Handled via bulk changeset

#### **Why Skip Deletion Tasks for New Systems?**

When setting up a new system, the tool compares API data with existing OSM data. Any stations in OSM but not in the API would normally create "removal" tasks. However, for new systems:

- ??? **Protects Existing Work**: Preserves bike share stations already mapped by the community
- ?? **Avoids Data Loss**: Prevents accidental deletion of valid OSM data
- ?? **Focuses on Gaps**: Concentrates on adding missing stations rather than removing existing ones
- ?? **Gradual Integration**: Allows manual review of what should be removed vs. what should stay

**Example Output for New System:**
```
?? Detected new system setup - skipping 'removed' task creation to avoid deleting existing OSM data.
   Only 'added' and 'moved' tasks will be created for stations missing from or different in OSM.

??  Skipping 'removed' challenge creation for new system to preserve existing OSM data.
Creating added challenge: Capital Bikeshare -- Added stations at 2024-01-15 since 2024-01-15
Creating moved challenge: Capital Bikeshare -- Moved stations at 2024-01-15 since 2024-01-15