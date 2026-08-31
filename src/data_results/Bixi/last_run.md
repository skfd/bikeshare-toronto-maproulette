# Bixi (Montréal)

Run at 2026-08-31 23:35 UTC — succeeded

| Change | Count |
|---|---:|
| Added vs last baseline | 58 |
| Removed vs last baseline | 11 |
| Moved vs last baseline | 124 |
| Renamed vs last baseline | 19 |
| Missing in OSM | 66 |
| Extra in OSM | 91 |
| Moved vs OSM | 128 |
| Renamed vs OSM | 22 |
| Closed in GBFS | 7 |
| Ref conflicts | 84 |

## Next steps

- [ ] Load data_results/Bixi/bikeshare_renames.osc in JOSM, verify, and upload — 22 rename(s) pending.
- [ ] Review data_results/Bixi/bikeshare_ref_conflicts.geojson — 84 OSM node(s) with a stale/recycled ref to fix manually.
- [ ] Review data_results/Bixi/bikeshare_closed.geojson — 7 station(s) closed in GBFS; check whether any need disused:amenity in OSM.
- [ ] Complete the MapRoulette duplicate-ref tasks created for Bixi.
- [ ] Complete MapRoulette tasks for 66 station(s) missing in OSM.
- [ ] Review bikeshare_extra_in_osm.geojson — 91 OSM station(s) not present in GBFS.
- [ ] Review bikeshare_moved_in_osm.geojson — 128 station(s) moved vs OSM.
- [ ] Commit updated data_results/Bixi/bikeshare.geojson as the next baseline.
