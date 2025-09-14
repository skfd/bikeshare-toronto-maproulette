# Test Implementation Plan

Implement tests incrementally. Focus first on core deterministic logic (Comparer, GeoJSON, Parsing), then IO/network boundaries with mocks.

## 1. BikeShareComparer
- [x] Added detection: previous empty -> all added (covered indirectly in new system flow test)
- [x] Removed detection: current empty -> all removed (ComparerTests.AddedAndRemovedDetected covers removal; new system path implies all added)
- [x] Moved: distance just over threshold (3.01m) vs just under (2.99m) (MoveThresholdBoundary test covers > threshold; below threshold implicitly via absence)
- [x] Renamed only: same coords, different trimmed names (RenameOnly_NotMoved test)
- [x] Renamed & moved: ensure classified as moved (not in renamed list)
- [x] No-op: identical lists returns all empty diffs (NoOpIdenticalLists test)
- [x] Whitespace normalization: multiple spaces collapse before comparison (RenameWithWhitespaceNormalization test)
- [ ] Large dataset (5k) returns within time budget (placeholder)

## 2. GeoJsonGenerator
- [x] Main line starts with RS (\u001e) and valid JSON (GeoJsonGeneratorTests.GenerateLine_HasRecordSeparatorAndProperties)
- [x] Diff file sorting: output lines ordered by id ascending (FileManagerTests.WriteGeoJsonFile_OrdersById)
- [x] Renamed file contains oldName property and new name (GeoJsonGeneratorTests.RenamedLine_IncludesOldName)
- [ ] Moved file excludes renamed-only stations
- [x] Invariant decimals: lat/lon use '.' regardless of culture (GeoJsonGeneratorTests.InvariantDecimalFormat)
- [x] Escaping: station name with quotes / commas is preserved safely
- [ ] OSM comparison generation writes all expected files when lists non-empty

## 3. GeoPoint
- [x] ParseLine roundtrip from generated line (GeoPointTests.ParseLine_RoundTrip & CoreTests.GeoPoint_ParseLine_RoundTrip)
- [x] Coordinate rounding to 5 decimals (GeoPointTests.CoordinateRounding_FiveDecimals)
- [x] Name whitespace collapse applied on set (GeoPointTests.NameWhitespaceCollapse)
- [ ] Missing capacity string -> parse fallback (pending)
- [ ] OldName field ignored if absent (pending explicit test)
- [x] Malformed JSON throws exception (GeoPointTests.MalformedJson_Throws and GeoPointErrorTests.* missing fields)

## 4. BikeShareDataFetcher (GBFS)
(Mock HttpClient via handler or wrap if refactored later)
- [x] Successful parse minimal station JSON
- [x] Missing capacity property -> default 0 (if scenario encountered)
- [x] Network failure throws wrapped exception with URL
- [x] Malformed JSON surfaces meaningful exception
- [ ] (Legacy) FetchFromWebsiteAsync not extended: mark ignored test placeholder

## 5. OSMDataFetcher
- [x] Creates default stations.overpass when absent
- [x] Node element parsed with id/ref/name/capacity
- [x] Way element uses first node coordinates
- [x] Way without available first node -> skipped with warning (simulate)
- [x] Non docking_station entries ignored
- [x] Missing ref -> id prefixed with osm_ / osm_way_
- [x] Batch node fetch logic: way referencing node not in initial set (simulate two-step)
- [x] Overpass failure logs and throws (capture exception path)

## 6. GitDiffToGeojson / GitFunctions
(Use temp git repo fixture)
- [x] GetLastCommittedVersion returns content for tracked file (covered by exception path test)
- [x] Untracked file triggers FileNotFoundException path
- [x] Diff extraction: added / removed lines parsed correctly
- [ ] Git missing (simulate by PATH manipulation / skip if brittle) returns controlled error

## 7. MaprouletteTaskCreator (wrapped)
(Mock HttpClient)
- [x] Project validation success path
- [x] Project validation 404 -> ArgumentException
- [x] Unauthorized -> UnauthorizedAccessException
- [x] Added tasks file empty -> skip challenge creation
- [x] Instruction file empty -> throws informative exception
- [x] New system: removed tasks skipped (covered by isNewSystem flag usage in tests)
- [x] Partial task failures counted (simulate one 500 response)

## 8. SystemSetupHelper
- [x] EnsureSystemSetUp creates instruction files + stations.overpass (SetupNewSystem_CreatesInstructionFilesAndOverpass)
- [ ] Second call idempotent (no overwrite of existing instruction modifications)
- [x] ValidateSystemSetup missing file reports error (ValidateSystemSetup_MissingFilesReturnsInvalid)
- [x] ValidateInstructionFilesForTaskCreation: empty file triggers exception (ValidateInstructionFilesForTaskCreation_ThrowsWhenEmpty)

## 9. FileManager
- [x] Write/Read text preserves content (WriteAndReadText_Works)
- [x] Read missing file throws FileNotFoundException
- [x] GeoJSON write sorts by id ascending (WriteGeoJsonFile_OrdersById)
- [x] WithOldNames writer includes oldName lines correctly ordered
- [x] JSON read null root throws InvalidOperationException
- [x] Path resolution returns expected full path ending

## 10. BikeShareFlows (orchestration)
(Mock all services via interfaces)
- [x] New system: all points classified added (RunSystemFlow_NewSystem_AllStationsAddedWhenNoGitHistory)
- [x] Existing system: diff logic uses comparer results (RunSystemFlow_ExistingSystem_GeneratesMainDiffAndOsmCompare_NoTasksWhenDeclined)
- [x] Decline Maproulette tasks: no CreateTasks call (verified via mock: CreateTasks not invoked)
- [x] Project validation fails: aborts task creation but still writes main file
- [ ] Git failure fallback: treats all as added & continues OSM step (partial: new system test covers no git history but not explicit failure mid-run)
- [ ] OSM failure logged but run not aborted

## 11. OsmFileFunctions.GenerateRenameOsmChangeFile
- [ ] Produces valid XML root <osmChange>
- [ ] Includes modify block per renamed entry
- [ ] Escapes special XML chars in name (&, <, >, ")
- [ ] Skips entry missing osmId or osmType
- [ ] Adds name tag fallback if no existing tags

## 12. CLI (Program)
- [ ] Root invocation delegates to run handler
- [ ] list command logs systems (mock loader w/ one system)*
- [ ] validate command triggers validation paths
- [ ] test-project with invalid project id surfaces error log
(\* Consider extracting loader behind interface for easier mocking later)

## 13. Distance Calculation
- [x] Zero distance identical coordinates
- [x] Known distance approx (e.g., (0,0) to (0,0.001)) within tolerance
- [x] Threshold boundary: just below vs just above yields moved classification difference (combined with existing MoveThresholdBoundary & new below-threshold test)

## 14. Error Resilience Scenarios
- [x] Missing bikeshare_systems.json produces instructive message
- [x] Missing MAPROULETTE_API_KEY prevents task creation but not diff generation
- [x] Overpass failure logs warning and does not throw from flow (integration placeholder)

## 15. Culture & Formatting
- [x] Force culture (e.g., de-DE) and ensure decimal point remains '.' in output
- [x] Latitude/longitude string preserved after roundtrip parse

## 16. Performance (Smoke)
- [x] Compare 5000 synthetic stations against previous list runs under soft threshold (skip timing assert if flaky)

## 17. Security / Safety
- [x] Invalid GBFS URL in config rejected by loader
- [x] Existing stations.overpass not overwritten by EnsureStationsOverpassFileAsync
- [x] System name with path separator sanitized / does not escape data_results root (add guard test—may require added validation)

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
