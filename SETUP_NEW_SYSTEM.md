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

### 3. **System-Specific Processing**
- Fetches data from your GBFS API
- Creates system-specific GeoJSON files
- Handles git integration for change tracking
- Generates OSM comparison files

## Step-by-Step Setup Example

### Example: Adding Capital Bikeshare

1. **Add to bikeshare_systems.json:**
```json
[
  {
    "id": 1,
    "name": "Bike Share Toronto",
    "city": "Toronto",
    "maproulette_project_id": 60735,
    "gbfs_api": "https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information.json"
  },
  {
    "id": 2,
    "name": "Bixi",
    "city": "Montreal", 
    "maproulette_project_id": -1,
    "gbfs_api": "https://gbfs.velobixi.com/gbfs/2-2/en/station_information.json"
  },
  {
    "id": 3,
    "name": "Capital Bikeshare",
    "city": "Washington DC",
    "maproulette_project_id": 54321,
    "gbfs_api": "https://gbfs.capitalbikeshare.com/gbfs/en/station_information.json"
  }
]
```

2. **Run the tool:**
```bash
dotnet run 3
```

3. **First run output:**
```
System Capital Bikeshare is not fully set up. Creating missing instruction files...
Setting up new system: Capital Bikeshare
Created system directory: C:\...\data_results\Capital Bikeshare
Created instructions directory: C:\...\data_results\Capital Bikeshare\instructions
Created instruction file: added.md
Created instruction file: removed.md  
Created instruction file: moved.md
Created instruction file: renamed.md
Successfully set up new system: Capital Bikeshare

No previous bikeshare.geojson file found in git history. This appears to be a new system setup.
Using current date as the reference point for changes.

Fetching bike share data from: https://gbfs.capitalbikeshare.com/gbfs/en/station_information.json
Successfully fetched 587 bike share stations
Generating main geojson file...
Main geojson file saved.

No previous version found in git repository. This appears to be a new system.
Treating all current stations as newly added.
Generated diff files for new system: 587 stations marked as added, 0 removed, 0 moved, 0 renamed

Fetching current bikeshare stations from OpenStreetMap for Washington DC...
Found 423 bikeshare stations in OSM
OSM comparison: 164 missing in OSM, 0 extra in OSM, 0 moved in OSM, 0 renamed in OSM
```

## Finding Your GBFS API Endpoint

Most bike share systems follow the GBFS (General Bikeshare Feed Specification) standard:

### 1. **Check System Website**
Look for "Open Data", "API", or "Developer" sections

### 2. **Common GBFS Patterns**
- `https://gbfs.{system-domain}.com/gbfs/en/station_information.json`
- `https://{system-domain}.com/gbfs/v1/en/station_information.json`
- `https://api.{system-domain}.com/station_information.json`

### 3. **GBFS Discovery**
Some systems provide discovery URLs:
- Look for `gbfs.json` file at system domain
- Check [GBFS Systems List](https://github.com/NABSA/gbfs/blob/master/systems.csv)

### 4. **Test Your URL**
Your endpoint should return JSON with this structure:
```json
{
  "data": {
    "stations": [
      {
        "station_id": "123",
        "name": "Station Name",
        "lat": 43.123456,
        "lon": -79.123456,
        "capacity": 15
      }
    ]
  }
}
```

## Customizing Instruction Templates

After automatic setup, you can customize the instruction files in:
`data_results/Your System Name/instructions/`

### Common Customizations

**For specific operators:**
```markdown
operator=City Transportation Authority
operator:type=public
operator:wikidata=Q12345678
```

**For private systems:**
```markdown
operator=Private Company
operator:type=private
```

**For systems with networks:**
```markdown
brand=System Brand
network=Network Name
network:wikidata=Q87654321
```

## Maproulette Integration

### Setting Up Maproulette Project

1. **Create Project** at [maproulette.org](https://maproulette.org)
2. **Get Project ID** from URL: `https://maproulette.org/admin/project/{ID}`
3. **Update bikeshare_systems.json** with the project ID
4. **Set Environment Variable:**
   ```bash
   set MAPROULETTE_API_KEY=your_api_key_here
   ```

### Without Maproulette

Set `maproulette_project_id` to `-1` to skip task creation:
```json
{
  "maproulette_project_id": -1
}
```

## Troubleshooting

### Common Issues

**1. "Configuration file not found"**
- Ensure `bikeshare_systems.json` exists in project root
- Check file permissions

**2. "System with ID X not found"**
- Verify ID exists in `bikeshare_systems.json`
- Use `dotnet run --list` to see available systems

**3. "Failed to fetch bike share data"**
- Test GBFS URL in browser
- Check URL format and network connectivity
- Verify API is publicly accessible

**4. "No previous version found in git repository"**
- This is normal for new systems
- All stations will be marked as "added" on first run

### Validation

Run system validation:
```bash
# List all systems and their status
dotnet run --list

# Run tool to auto-validate and setup
dotnet run <system-id>
```

### Getting Help

The tool provides helpful error messages and will:
- ? Auto-create missing directories
- ? Generate missing instruction files  
- ? Handle new system setup gracefully
- ? Provide clear error messages for missing files

## Advanced Configuration

### Multiple Cities

For systems covering multiple cities, use the primary city:
```json
{
  "name": "Bay Area Bike Share",
  "city": "San Francisco"
}
```

### Custom File Organization

The tool organizes files by system name:
- Use descriptive, filesystem-safe names
- Avoid special characters: `/ \ : * ? " < > |`
- Spaces are converted to valid directory names

### Git Integration

The tool automatically:
- Tracks changes in git
- Compares with previous versions
- Handles new systems without git history
- Maintains separate history per system

## Examples

### Real-World Systems

**Citi Bike NYC:**
```json
{
  "id": 4,
  "name": "Citi Bike",
  "city": "New York City", 
  "maproulette_project_id": -1,
  "gbfs_api": "https://gbfs.citibikenyc.com/gbfs/en/station_information.json"
}
```

**Divvy Chicago:**
```json
{
  "id": 5,
  "name": "Divvy",
  "city": "Chicago",
  "maproulette_project_id": -1, 
  "gbfs_api": "https://gbfs.divvybikes.com/gbfs/en/station_information.json"
}
```

**Ford GoBike:**
```json
{
  "id": 6,
  "name": "Bay Wheels",
  "city": "San Francisco",
  "maproulette_project_id": -1,
  "gbfs_api": "https://gbfs.baywheels.com/gbfs/en/station_information.json"
}
```

---

**That's it!** The tool handles everything else automatically. Just add your configuration and run!