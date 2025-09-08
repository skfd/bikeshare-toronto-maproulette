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
- ? **Custom Overpass Queries**: Creates `stations.overpass` file for precise OSM data fetching

### 5. **Smart OSM Data Fetching**
The tool now uses system-specific Overpass query files instead of generating queries inline:

**File Location:** `data_results/SYSTEM_NAME/stations.overpass`

**Benefits:**
- ?? **Precise Control**: Customize exactly which areas and criteria to search
- ?? **Complex Queries**: Support for multi-area queries (like Bixi's Montreal + Sherbrooke)
- ?? **Version Control**: Overpass queries are tracked in git with your data
- ? **Consistent Results**: Same query used across all runs for reproducible results

**Auto-Generated for New Systems:**
```overpass
[out:json];

area[name="Your City"]->.city;
(
    node(area.city)[bicycle_rental=docking_station];
    way(area.city)[bicycle_rental=docking_station];
    relation(area.city)[bicycle_rental=docking_station];
);

out meta;
```

**Custom Example (Bixi Montreal):**
```overpass
[out:json];

(
  area[name="Montréal (Région métropolitaine de recensement)"];
  area[name="Sherbrooke"];
)->.city;
(
  node(area.city)[bicycle_rental=docking_station];
  way(area.city)[bicycle_rental=docking_station];
  area(area.city)[bicycle_rental=docking_station];
);

out meta;
```

### Custom File Organization

The tool organizes files by system name:
- Use descriptive, filesystem-safe names
- Avoid special characters: `/ \ : * ? " < > |`
- Spaces are converted to valid directory names

### Customizing Overpass Queries

Each system can have a custom `stations.overpass` file for precise OSM data fetching:

**Location:** `data_results/SYSTEM_NAME/stations.overpass`

**Common Customizations:**

**Multi-City Systems:**
```overpass
[out:json];

(
  area[name="City 1"];
  area[name="City 2"];
  area[name="City 3"];
)->.searcharea;
(
  node(area.searcharea)[bicycle_rental=docking_station];
  way(area.searcharea)[bicycle_rental=docking_station];
  relation(area.searcharea)[bicycle_rental=docking_station];
);

out meta;
```

**Specific Operators:**
```overpass
[out:json];

area[name="Your City"]->.city;
(
  node(area.city)[bicycle_rental=docking_station][operator="Your Operator"];
  way(area.city)[bicycle_rental=docking_station][operator="Your Operator"];
);

out meta;
```

**Regional Systems:**
```overpass
[out:json];

(
  area[name~"Greater.*Area"];
  area[name~".*Metropolitan.*"];
)->.region;
(
  node(area.region)[bicycle_rental=docking_station];
  way(area.region)[bicycle_rental=docking_station];
);

out meta;
```

**Testing Your Query:**
1. Edit `data_results/SYSTEM_NAME/stations.overpass`
2. Test at [Overpass Turbo](https://overpass-turbo.eu/)
3. Run the tool to see results: `dotnet run <system-id>`