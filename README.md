See [Maproulette](https://mpr.lt/c/45294) challenge to bring the map of Bikeshare stations up to date!


query to examine existing stations:
```
// @name Cycle Network

/*
This shows the cycleway and cycleroute network.
*/

[out:json];

(
  node[bicycle_rental=docking_station]  ({{bbox}});
  way[bicycle_rental=docking_station]  ({{bbox}});
  area[bicycle_rental=docking_station]  ({{bbox}});

);

out body;
>;
out skel qt;
```
