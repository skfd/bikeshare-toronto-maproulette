Hand made tool to automatically update the Bikeshare Toronto stations in OpenStreetMap. Current version can download information about Bikeshare stations from Vendor API, compare with OpenStreetMap data, and create Maproulette challenges. Future ambition is to automate this some more.

## New Features

### OSM Data Download and Comparison

The tool now supports downloading current bikeshare station data from OpenStreetMap using the Overpass API. This enables comparison between the official BikeShare Toronto API data and what's currently mapped in OpenStreetMap.

To enable OSM comparison, uncomment the following line in `Program.cs`:
```csharp
//await CompareWithOSMData(locationsList);
```

This will generate additional comparison files:
* `bikeshare_missing_in_osm.geojson`: Stations that exist in the API but are missing from OSM
* `bikeshare_extra_in_osm.geojson`: Stations that exist in OSM but not in the current API
* `bikeshare_moved_in_osm.geojson`: Stations that have different coordinates between API and OSM
* `bikeshare_renamed_in_osm.geojson`: Stations that have different names between API and OSM


See [Bike Share Toronto project](https://maproulette.org/admin/project/60735) on Maproulette to bring the map of Bikeshare stations up to date!

[Follow BikeShareTO socials to get pictures of new stations](https://x.com/BikeShareTO)

[query to examine existing stations](https://overpass-turbo.eu/s/1LGI)

[rogue stations without `ref` tag](https://overpass-turbo.eu/s/1QGK)

```
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

## Generated Files

The following files are generated and updated regularly:

* `bikeshare.geojson`: Contains all current Bikeshare Toronto stations.
* `bikeshare_added.geojson`: New stations added since the last update.
* `bikeshare_removed.geojson`: Stations removed since the last update.
* `bikeshare_moved.geojson`: Stations that have changed location, and maybe name too.
* `bikeshare_renamed.geojson`: Stations that have been renamed, but not moved.


## Docs for libraries and APIs

https://github.com/maproulette/maproulette-backend/blob/main/docs/challenge_api.md#manually-building-a-challenge