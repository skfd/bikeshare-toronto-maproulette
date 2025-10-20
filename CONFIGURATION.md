# Bike Share System Configuration Guide

This document describes all configuration options available in `bikeshare_systems.json`.

## Configuration File Location

- **File**: `src/bikeshare_systems.json`
- **Format**: JSON array of bike share system objects
- **Example**: See `src/bikeshare_systems.example.json`

## Required Fields

### `id` (integer)
Unique identifier for the system. Used in command-line arguments.

**Example**: `1`, `2`, `3`

### `name` (string)
Display name of the bike share system.

**Example**: `"Bike Share Toronto"`, `"Citi Bike"`

### `city` (string)
City or region where the system operates. Used in Overpass API queries and logging.

**Example**: `"Toronto"`, `"New York City"`, `"Montréal"`

### `gbfs_api` (string)
URL to the GBFS `station_information` endpoint. Must be a valid HTTP/HTTPS URL.

**Example**: `"https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information.json"`

## Optional Fields

### `maproulette_project_id` (integer)
MapRoulette project ID for creating mapping tasks. If not set or 0, MapRoulette task creation will be skipped.

**Example**: `60735`

**Default**: `0` (disabled)

### `brand:wikidata` (string)
Wikidata entity ID for the bike share brand. Used in instruction templates and tagging suggestions.

**Example**: `"Q17018523"` (for Bike Share Toronto)

**Default**: `null`

### `station_name_prefix` (string)
Prefix to automatically add to all station names. Useful when the bike share operator's data doesn't include the brand name in station names.

**Example**: `"Citi Bike - "`

**Result**: Station "Broadway & W 29th St" becomes "Citi Bike - Broadway & W 29th St"

**Default**: `null` (no prefix)

### `move_threshold_meters` (number)
Distance threshold in meters for detecting moved stations when comparing current GBFS data with previously committed data.

**Purpose**: Determines when a station is classified as "moved" vs "same location"

**Use cases**:
- **Increase (5-10m)**: If stations are frequently relocated by small amounts or GPS coordinates drift
- **Decrease (1-2m)**: If you want to detect even very small movements
- **Default is usually fine**: 3 meters is a good balance for most systems

**Example**: `5.0`

**Default**: `3.0` meters

### `osm_comparison_threshold_meters` (number)
Distance threshold in meters for matching stations when comparing GBFS data with OpenStreetMap data.

**Purpose**: Determines if a GBFS station and an OSM station represent the same physical location

**Use cases**:
- **Increase (50-100m)**: If OSM mapping is less precise, stations are in dense clusters, or you want more lenient matching
- **Decrease (10-20m)**: If OSM data is very accurate and you want stricter matching
- **Default is usually fine**: 30 meters accounts for typical GPS/mapping imprecision

**Example**: `50.0`

**Default**: `30.0` meters

## Complete Example

```json
[
  {
    "id": 1,
    "name": "Bike Share Toronto",
    "city": "Toronto",
    "maproulette_project_id": 60735,
    "gbfs_api": "https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information.json",
    "brand:wikidata": "Q17018523",
    "move_threshold_meters": 3.0,
    "osm_comparison_threshold_meters": 30.0
  },
  {
    "id": 2,
    "name": "Citi Bike",
    "city": "New York City",
    "maproulette_project_id": 60735,
    "gbfs_api": "https://gbfs.lyft.com/gbfs/2.3/bkn/en/station_information.json",
    "brand:wikidata": "Q2974438",
    "station_name_prefix": "Citi Bike - ",
    "move_threshold_meters": 5.0,
    "osm_comparison_threshold_meters": 50.0
  }
]
```

## Validation

The tool validates configuration on startup and provides helpful error messages:

- Duplicate IDs are detected
- Required fields are checked
- URLs are validated for proper format
- Threshold values must be positive numbers

## Adding a New System

1. Add a new object to the JSON array
2. Assign a unique `id`
3. Fill in required fields (`name`, `city`, `gbfs_api`)
4. Optionally configure thresholds and other settings
5. Run `dotnet run -- <id>` to scaffold system directories
6. Adjust generated `stations.overpass` query if needed

## Debugging Configuration

Use the verbose flag to see which thresholds are being used:

```bash
dotnet run -- <id> -v
```

Output will include:
```
[DEBUG] Using move threshold: 3m for Bike Share Toronto
[DEBUG] Using OSM comparison threshold: 30m for Bike Share Toronto
```

## Related Documentation

- **README.md**: Quick start and operator guide
- **SETUP_NEW_SYSTEM.md**: Detailed setup instructions
- **DEV_README.md**: Technical architecture and development guide
