See [Maproulette](https://maproulette.org/browse/projects/53785) project to bring the map of Bikeshare stations up to date!

[Follow BikeShareTO socials to get pictures of new stations](https://x.com/BikeShareTO)
[query to examine existing stations](https://overpass-turbo.eu/s/1LGI):
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
* `bikeshare_moved.geojson`: Stations that have changed location.
* `bikeshare_renamed.geojson`: Stations that have been renamed.


## Docs

https://github.com/maproulette/maproulette-backend/blob/main/docs/challenge_api.md#manually-building-a-challenge