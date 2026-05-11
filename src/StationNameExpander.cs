using System;
using System.Collections.Generic;

namespace prepareBikeParking
{
    // Expands abbreviated street tokens in station names per OSM convention
    // (https://wiki.openstreetmap.org/wiki/Abbreviations). Names like
    // "28 Ave & 44 St" become "28 Avenue & 44 Street".
    //
    // Rules:
    //   - Split on '&' (intersection separator); each side handled independently.
    //   - Suffix tokens (St, Ave, Rd, Blvd, …) expand at any position.
    //   - Direction tokens (N, S, E, W) expand only at the first or last position
    //     of a side, so a middle "S." (initial in "Thomas S. Boyland St") is
    //     left alone.
    //   - A leading "St" on a side is preserved as Saint (e.g. "St Marks Pl",
    //     "St Nicholas Ave") — not expanded to Street.
    //   - Period suffix on a token (e.g. "S.") is NOT stripped; only exact
    //     short forms match. Initials like "S." therefore never expand.
    public static class StationNameExpander
    {
        private static readonly Dictionary<string, string> SuffixExpand = new(StringComparer.OrdinalIgnoreCase)
        {
            { "St", "Street" }, { "Rd", "Road" }, { "Ave", "Avenue" }, { "Blvd", "Boulevard" },
            { "Dr", "Drive" }, { "Ln", "Lane" }, { "Ct", "Court" }, { "Crt", "Court" },
            { "Pl", "Place" }, { "Ter", "Terrace" }, { "Cres", "Crescent" }, { "Sq", "Square" },
            { "Gte", "Gate" }, { "Gt", "Gate" }, { "Cir", "Circle" }, { "Crcl", "Circle" },
            { "Trl", "Trail" }, { "Pkwy", "Parkway" }, { "Hwy", "Highway" }, { "Expy", "Expressway" },
            { "Gdns", "Gardens" }, { "Grv", "Grove" }, { "Hts", "Heights" },
            { "Ptwy", "Pathway" }, { "Crct", "Circuit" }, { "Bdge", "Bridge" }, { "Lwn", "Lawn" },
            { "Pk", "Park" }, { "Rdwy", "Roadway" }, { "Cs", "Close" }, { "Wds", "Woods" },
            { "Grn", "Green" },
        };

        private static readonly Dictionary<string, string> DirExpand = new(StringComparer.OrdinalIgnoreCase)
        {
            { "N", "North" }, { "S", "South" }, { "E", "East" }, { "W", "West" },
        };

        public static int Apply(IEnumerable<GeoPoint> points, bool enabled)
        {
            if (!enabled) return 0;
            var changed = 0;
            foreach (var p in points)
            {
                if (string.IsNullOrEmpty(p.name)) continue;
                var expanded = ExpandName(p.name);
                if (!string.Equals(expanded, p.name, StringComparison.Ordinal))
                {
                    p.name = expanded;
                    changed++;
                }
            }
            return changed;
        }

        public static string ExpandName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sides = name.Split('&');
            for (int i = 0; i < sides.Length; i++)
            {
                sides[i] = ExpandSide(sides[i].Trim());
            }
            return string.Join(" & ", sides);
        }

        private static string ExpandSide(string side)
        {
            if (string.IsNullOrEmpty(side)) return side;
            var tokens = side.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return side;

            var preserveLeadingSt = string.Equals(tokens[0], "St", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < tokens.Length; i++)
            {
                if (i == 0 && preserveLeadingSt) continue;

                if (SuffixExpand.TryGetValue(tokens[i], out var sExpanded))
                {
                    tokens[i] = sExpanded;
                    continue;
                }

                bool atEdge = (i == 0) || (i == tokens.Length - 1);
                if (atEdge && DirExpand.TryGetValue(tokens[i], out var dExpanded))
                {
                    tokens[i] = dExpanded;
                }
            }
            return string.Join(' ', tokens);
        }
    }
}
