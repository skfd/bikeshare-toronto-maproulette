# Test Implementation Plan

Implement tests incrementally. Focus first on core deterministic logic (Comparer, GeoJSON, Parsing), then IO/network boundaries with mocks.

## 1. BikeShareComparer
- Added detection: previous empty -> all added
- Removed detection: current empty -> all removed
- Moved: distance just over threshold (3.01m) vs just under (2.99m)
- Renamed only: same coords, different trimmed names ("Station  A" → "Station A")
- Renamed & moved: ensure classified as moved (not in renamed list)
- No-op: identical lists returns all empty diffs
- Whitespace normalization: multiple spaces collapse before comparison
- Large dataset (5k) returns within time budget (no assertion on timing yet; placeholder)

## 2. GeoJsonGenerator
- Main line starts with RS (\u001e) and valid JSON
- Diff file sorting: output lines ordered by id ascending
- Renamed file contains oldName property and new name
- Moved file excludes renamed-only stations
- Invariant decimals: lat/lon use '.' regardless of culture
- Escaping: station name with quotes / commas is preserved safely
- OSM comparison generation writes all expected files when lists non-empty

## 3. GeoPoint
- ParseLine roundtrip from generated line
- Coordinate rounding to 5 decimals (input with longer precision)
- Name whitespace collapse applied on set
- Missing capacity string -> parse fallback (provide line with capacity "0")
- OldName field ignored if absent (renamed lines still parse core fields)
- Malformed JSON throws exception

## 4. BikeShareDataFetcher (GBFS)
(Mock HttpClient via handler or wrap if refactored later)
- Successful parse minimal station JSON
- Missing capacity property -> default 0 (if scenario encountered)
- Network failure throws wrapped exception with URL
- Malformed JSON surfaces meaningful exception
- (Legacy) FetchFromWebsiteAsync not extended: mark ignored test placeholder

## 5. OSMDataFetcher
- Creates default stations.overpass when absent
- Node element parsed with id/ref/name/capacity
- Way element uses first node coordinates
- Way without available first node -> skipped with warning (simulate)
- Non docking_station entries ignored
- Missing ref -> id prefixed with osm_ / osm_way_
- Batch node fetch logic: way referencing node not in initial set (simulate two-step)
- Overpass failure logs and throws (capture exception path)

## 6. GitDiffToGeojson / GitFunctions
(Use temp git repo fixture)
- GetLastCommittedVersion returns content for tracked file
- Untracked file triggers FileNotFoundException path
- Diff extraction: added / removed lines parsed correctly
- Git missing (simulate by PATH manipulation / skip if brittle) returns controlled error

## 7. MaprouletteTaskCreator (wrapped)
(Mock HttpClient)
- Project validation success path
- Project validation 404 -> ArgumentException
- Unauthorized -> UnauthorizedAccessException
- Added tasks file empty -> skip challenge creation
- Instruction file empty -> throws informative exception
- New system: removed tasks skipped
- Partial task failures counted (simulate one 500 response)

## 8. SystemSetupHelper
- EnsureSystemSetUp creates instruction files + stations.overpass
- Second call idempotent (no overwrite of existing instruction modifications)
- ValidateSystemSetup missing file reports error
- ValidateInstructionFilesForTaskCreation: empty file triggers exception

## 9. FileManager
- Write/Read text preserves content
- Read missing file throws FileNotFoundException
- GeoJSON write sorts by id ascending
- WithOldNames writer includes oldName lines correctly ordered
- JSON read null root throws InvalidOperationException
- Path resolution returns expected full path ending

## 10. BikeShareFlows (orchestration)
(Mock all services via interfaces)
- New system: all points classified added
- Existing system: diff logic uses comparer results
- Decline Maproulette tasks: no CreateTasks call
- Project validation fails: aborts task creation but still writes main file
- Git failure fallback: treats all as added & continues OSM step
- OSM failure logged but run not aborted

## 11. OsmFileFunctions.GenerateRenameOsmChangeFile
- Produces valid XML root <osmChange>
- Includes modify block per renamed entry
- Escapes special XML chars in name (&, <, >, ")
- Skips entry missing osmId or osmType
- Adds name tag fallback if no existing tags

## 12. CLI (Program)
- Root invocation delegates to run handler
- list command logs systems (mock loader w/ one system)\*
- validate command triggers validation paths
- test-project with invalid project id surfaces error log
(\* Consider extracting loader behind interface for easier mocking later)

## 13. Distance Calculation
- Zero distance identical coordinates
- Known distance approx (e.g., (0,0) to (0,0.001)) within tolerance
- Threshold boundary: just below vs just above yields moved classification difference

## 14. Error Resilience Scenarios
- Missing bikeshare_systems.json produces instructive message
- Missing MAPROULETTE_API_KEY prevents task creation but not diff generation
- Overpass failure logs warning and does not throw from flow

## 15. Culture & Formatting
- Force culture (e.g., de-DE) and ensure decimal point remains '.' in output
- Latitude/longitude string preserved after roundtrip parse

## 16. Performance (Smoke)
- Compare 5000 synthetic stations against previous list runs under soft threshold (skip timing assert if flaky)

## 17. Security / Safety
- Invalid GBFS URL in config rejected by loader
- Existing stations.overpass not overwritten by EnsureStationsOverpassFileAsync
- System name with path separator sanitized / does not escape data_results root (add guard test—may require added validation)

---

## Suggested Implementation Order
1. Comparer / GeoPoint / GeoJsonGenerator
2. FileManager / SystemSetupHelper
3. OSM + GBFS fetchers (with lightweight Http mocks)
4. Flows orchestration (mocked dependencies)
5. Maproulette + Git interactions
6. Edge / performance / culture / security tests

## Notes
- Some areas (HttpClient) may benefit from introducing injectable HttpClientFactory for cleaner mocks.
- Performance tests should be marked Category=Performance to allow exclusion.
- XML validation can use simple string assertions; full schema not required initially.

Feel free to adjust scope before implementation begins.
