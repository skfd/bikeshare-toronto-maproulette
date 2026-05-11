using System.Globalization;
using System.Text.RegularExpressions;

namespace prepareBikeParking
{
    /// <summary>
    /// Detects "ref conflicts" between GBFS and OSM: cases where the <c>ref</c> tag on an OSM
    /// bicycle_rental element no longer points at the station the GBFS feed currently uses that
    /// id for - typically because the operator recycled a station id. Two flavours:
    /// <list type="bullet">
    ///   <item><b>fix-ref</b>: an OSM node with no <c>ref</c> that, by exact name + proximity,
    ///   clearly is a current GBFS station. We can confidently add the missing <c>ref</c>/<c>ref:gbfs</c>.</item>
    ///   <item><b>review-ref</b>: an OSM node whose <c>ref</c> matches a GBFS id but the GBFS station
    ///   with that id is implausibly far away. We don't auto-edit it (its correct id is unclear and
    ///   changing it risks a duplicate); the operator resolves it manually.</item>
    /// </list>
    /// Either way the affected GBFS ids are reported so callers can suppress the bogus "moved"/"missing"
    /// entries the id-based comparison would otherwise produce.
    /// </summary>
    public static class RefConflictDetector
    {
        public sealed record RefConflict(
            GeoPoint OsmNode,
            string? CurrentRef,
            GeoPoint? ResolvedGbfs,
            double? ResolvedDistanceMeters,
            string? ClaimedGbfsName,
            double? ClaimedDistanceMeters,
            string Kind /* "fix-ref" | "review-ref" */);

        public sealed record RefConflictResult(
            IReadOnlyList<RefConflict> Conflicts,
            IReadOnlySet<string> SuppressedGbfsIds,
            IReadOnlyList<string> Warnings)
        {
            public IEnumerable<RefConflict> AutoFixes => Conflicts.Where(c => c.Kind == "fix-ref");
        }

        private static readonly RefConflictResult Empty =
            new(Array.Empty<RefConflict>(), new HashSet<string>(), Array.Empty<string>());

        public static RefConflictResult Detect(
            List<GeoPoint> gbfsPoints,
            List<GeoPoint> osmPoints,
            double osmComparisonThresholdMeters,
            double refConflictThresholdMeters)
        {
            if (gbfsPoints == null || osmPoints == null || gbfsPoints.Count == 0 || osmPoints.Count == 0)
                return Empty;

            var gbfsById = new Dictionary<string, GeoPoint>();
            foreach (var g in gbfsPoints)
            {
                if (!string.IsNullOrEmpty(g.id)) gbfsById[g.id] = g;
            }

            var gbfsByName = new Dictionary<string, List<GeoPoint>>();
            foreach (var g in gbfsPoints)
            {
                var nn = NormalizeName(g.name);
                if (nn.Length == 0) continue;
                if (!gbfsByName.TryGetValue(nn, out var list)) gbfsByName[nn] = list = new List<GeoPoint>();
                list.Add(g);
            }

            var conflicts = new List<RefConflict>();
            var warnings = new List<string>();
            var suppressed = new HashSet<string>();

            // Pass 1: OSM elements that already carry a ref but it looks stale (recycled id).
            foreach (var osm in osmPoints)
            {
                if (osm.IsDisused) continue;
                var currentRef = osm.id;
                if (string.IsNullOrEmpty(currentRef) || currentRef.StartsWith("osm_")) continue; // handled in pass 2
                if (!gbfsById.TryGetValue(currentRef, out var claimed)) continue; // ref not in GBFS at all -> already "extra in OSM"

                var claimedDist = Distance(osm, claimed);
                if (claimedDist <= refConflictThresholdMeters) continue; // close enough -> a real move, not a conflict

                // Best-effort: say what it actually is, by name + proximity.
                GeoPoint? resolved = null;
                double? resolvedDist = null;
                var nn = NormalizeName(osm.name);
                if (nn.Length > 0 && gbfsByName.TryGetValue(nn, out var named))
                {
                    var within = named.Where(g => Distance(osm, g) <= osmComparisonThresholdMeters).ToList();
                    if (within.Count == 1)
                    {
                        resolved = within[0];
                        resolvedDist = Distance(osm, resolved);
                    }
                }

                conflicts.Add(new RefConflict(osm, currentRef, resolved, resolvedDist,
                    claimed.name, claimedDist, "review-ref"));
                suppressed.Add(currentRef);
            }

            // Pass 2: OSM elements with no ref that, by exact name + proximity, are a current GBFS station.
            // These are the ones the comparer flags as "extra in OSM" (false deletion candidates).
            var orphanCandidates = new List<(GeoPoint osm, GeoPoint gbfs, double dist)>();
            foreach (var osm in osmPoints)
            {
                if (osm.IsDisused) continue;
                if (!string.IsNullOrEmpty(osm.id) && !osm.id.StartsWith("osm_")) continue; // has a ref -> pass 1
                if (string.IsNullOrEmpty(osm.osmId) || string.IsNullOrEmpty(osm.osmType)) continue; // can't emit an .osc <modify>

                var nn = NormalizeName(osm.name);
                if (nn.Length == 0) continue;
                if (!gbfsByName.TryGetValue(nn, out var named)) continue;

                var within = named.Where(g => Distance(osm, g) <= osmComparisonThresholdMeters).ToList();
                if (within.Count == 0) continue; // a same-named station exists but it's far -> leave alone
                if (within.Count > 1)
                {
                    warnings.Add($"OSM {osm.osmType}/{osm.osmId} \"{osm.name}\" has no ref and matches {within.Count} GBFS stations within {osmComparisonThresholdMeters:F0}m by name - skipping (ambiguous).");
                    continue;
                }
                var match = within[0];
                if (string.IsNullOrEmpty(match.id)) continue;
                orphanCandidates.Add((osm, match, Distance(osm, match)));
            }

            // Don't auto-assign the same GBFS id to two different OSM nodes.
            foreach (var group in orphanCandidates.GroupBy(c => c.gbfs.id))
            {
                var list = group.ToList();
                if (list.Count > 1)
                {
                    warnings.Add($"GBFS station {group.Key} matches {list.Count} ref-less OSM nodes by name+proximity - skipping all (ambiguous).");
                    continue;
                }
                var (osm, gbfs, dist) = list[0];
                conflicts.Add(new RefConflict(osm, null, gbfs, dist, null, null, "fix-ref"));
                suppressed.Add(gbfs.id);
            }

            if (conflicts.Count == 0 && warnings.Count == 0) return Empty;
            return new RefConflictResult(conflicts, suppressed, warnings);
        }

        private static double Distance(GeoPoint a, GeoPoint b) =>
            BikeShareComparer.GetDistanceInMeters(
                double.Parse(a.lat, CultureInfo.InvariantCulture),
                double.Parse(a.lon, CultureInfo.InvariantCulture),
                double.Parse(b.lat, CultureInfo.InvariantCulture),
                double.Parse(b.lon, CultureInfo.InvariantCulture));

        private static string NormalizeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim().ToLowerInvariant()
                .Replace('’', '\'')   // right single quote
                .Replace('‘', '\'');  // left single quote
            return Regex.Replace(t, @"\s+", " ");  // \s also collapses NBSP / tabs
        }
    }
}
