using System;
using System.Collections.Generic;

namespace prepareBikeParking
{
    public static class StationNamePrefixer
    {
        public static int Apply(IEnumerable<GeoPoint> points, string? prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return 0;
            var applied = 0;
            foreach (var p in points)
            {
                if (!string.IsNullOrEmpty(p.name) && !p.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    p.name = prefix + p.name;
                    applied++;
                }
            }
            return applied;
        }
    }
}
