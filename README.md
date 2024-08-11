See [Maproulette](https://mpr.lt/c/45294) challenge to bring the map of Bikeshare stations up to date!


Current state is in `bikeshare_stations.geojson`.

* Weekly points to review: `bikeshare_toreview.geojson`.
* Weekly points to remove: `bikeshare_removed.geojson`.

TODO:
	- Auto-creator of Maproulette challenges

[Follow BikeShareTO socials to get pictures of new stations](https://x.com/BikeShareTO)
[query to examine existing stations](https://overpass-turbo.eu/s/1LGI):
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
