# Bike Share Location Comparison Tool

A comprehensive tool to automatically update bike share stations in OpenStreetMap. This tool can download station information from bike share APIs, compare with OpenStreetMap data, and create Maproulette challenges for community mapping.

## Quick Start

### List Available Systems
```bash
dotnet run --list
```

### Run for a Specific System
```bash
dotnet run 1  # Bike Share Toronto
dotnet run 2  # Bixi Montreal
```

### Add New Systems
See **[SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md)** for complete instructions on adding new bike share systems.

## Features

### Multi-System Support
- **Bike Share Toronto** - Fully configured with Maproulette integration
- **Bixi Montreal** - Ready for OSM comparison and analysis
- **Any GBFS System** - Easy setup with automatic configuration

### OSM Data Download and Comparison

The tool supports downloading current bikeshare station data from OpenStreetMap using system-specific Overpass queries. Each system can have a customized `stations.overpass` file for precise control over OSM data fetching.

**Features:**
- ?? **Custom Overpass Queries**: Each system uses its own `data_results/SYSTEM_NAME/stations.overpass` file
- ?? **Multi-Area Support**: Complex queries for systems spanning multiple cities or regions  
- ?? **Version Controlled**: Overpass queries are tracked with your data for reproducible results
- ? **Auto-Generated**: Default queries created automatically for new systems

This enables comparison between official bike share API data and what's currently mapped in OpenStreetMap and generates:
* `bikeshare_missing_in_osm.geojson`: Stations that exist in the API but are missing from OSM
* `bikeshare_extra_in_osm.geojson`: Stations that exist in OSM but not in the current API
* `bikeshare_moved_in_osm.geojson`: Stations that have different coordinates between API and OSM
* `bikeshare_renamed_in_osm.geojson`: Stations that have different names between API and OSM
* `bikeshare_renames.osc`: Changeset file that can be uploaded to OSM using JOSM

### Automatic System Setup

When you add a new system to `bikeshare_systems.json`, the tool automatically:
- ? Creates system directory structure
- ? Generates instruction templates for Maproulette
- ? Handles first-time setup gracefully
- ? Provides helpful error messages
- ? **Protects existing OSM data** by skipping deletion tasks for new systems

## Generated Files

The tool generates files organized by system in `data_results/SYSTEM_NAME/`:

**Station Data:**
* `bikeshare.geojson`: Contains all current bike share stations
* `bikeshare_added.geojson`: New stations added since the last update
* `bikeshare_removed.geojson`: Stations removed since the last update
* `bikeshare_moved.geojson`: Stations that have changed location, and maybe name too
* `bikeshare_renamed.geojson`: Stations that have been renamed, but not moved

**OSM Comparison:**
* `bikeshare_missing_in_osm.geojson`: Stations missing from OpenStreetMap
* `bikeshare_extra_in_osm.geojson`: Extra stations in OpenStreetMap
* `bikeshare_moved_in_osm.geojson`: Stations with different coordinates in OSM
* `bikeshare_renamed_in_osm.geojson`: Stations with different names in OSM
* `bikeshare_renames.osc`: Changeset file for bulk name updates

**Configuration:**
* `stations.overpass`: Custom Overpass query for fetching OSM data
* `instructions/`: Maproulette instruction templates (added.md, removed.md, moved.md, renamed.md)

## Configuration

### Environment Variables
- `MAPROULETTE_API_KEY`: Required for creating Maproulette tasks

### System Configuration
Edit `bikeshare_systems.json` to add new systems. See [SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md) for detailed instructions.

## Examples

### Current Systems

**Bike Share Toronto:**
- See [Bike Share Toronto project](https://maproulette.org/admin/project/60735) on Maproulette
- [Follow BikeShareTO socials](https://x.com/BikeShareTO) for pictures of new stations

**Bixi Montreal:**
- Fully configured for API and OSM comparison
- Ready for Maproulette integration

### OSM Queries

**Examine existing stations:**
[Overpass query for Toronto stations](https://overpass-turbo.eu/s/1LGI)

**Find stations without ref tags:**
[Stations missing reference IDs](https://overpass-turbo.eu/s/1QGK)

```overpass
[out:json];

area[name="Toronto"]->.to;
(
  node(area.to)[bicycle_rental=docking_station];
  way(area.to)[bicycle_rental=docking_station];
  area(area.to)[bicycle_rental=docking_station];
);

out body;
>;
out skel qt;
```

## Troubleshooting

### Common Issues

**Configuration Errors:**
- Run `dotnet run --list` to validate your configuration
- Check `bikeshare_systems.json` syntax
- See [SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md) for examples

**API Errors:**
- Verify GBFS API URLs in a web browser
- Check internet connectivity
- Ensure API endpoints return valid JSON

**File Permissions:**
- Ensure write access to `data_results/` directory
- Check git repository status

The tool provides detailed error messages and troubleshooting guidance for most issues.

## Resources

- **Setup Guide**: [SETUP_NEW_SYSTEM.md](SETUP_NEW_SYSTEM.md)
- **Maproulette API**: [Challenge API Documentation](https://github.com/maproulette/maproulette-backend/blob/main/docs/challenge_api.md#manually-building-a-challenge)
- **GBFS Specification**: [General Bikeshare Feed Specification](https://github.com/NABSA/gbfs)
- **OpenStreetMap Wiki**: [Bicycle Rental Tagging](https://wiki.openstreetmap.org/wiki/Tag:amenity%3Dbicycle_rental)

## Contributing

1. Add your bike share system to `bikeshare_systems.json`
2. Test with `dotnet run <system-id>`
3. Submit a pull request with your configuration

The tool handles the rest automatically!