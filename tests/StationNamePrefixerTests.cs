using System.Collections.Generic;
using Xunit;

namespace prepareBikeParking.Tests
{
    public class StationNamePrefixerTests
    {
        private GeoPoint MakePoint(string id, string name) => new GeoPoint
        {
            id = id,
            name = name,
            latitude = 0,
            longitude = 0,
            address = id
        };

        [Fact]
        public void Apply_AddsPrefix_WhenMissing()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", "Alpha"),
                MakePoint("2", "Beta")
            };
            var count = StationNamePrefixer.Apply(pts, "Pre - ");
            Assert.Equal(2, count);
            Assert.All(pts, p => Assert.StartsWith("Pre - ", p.name));
        }

        [Fact]
        public void Apply_Skips_WhenAlreadyPrefixed_IgnoringCase()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", "Pre - Alpha"),
                MakePoint("2", "pre - Beta")
            };
            var count = StationNamePrefixer.Apply(pts, "Pre - ");
            Assert.Equal(0, count);
        }

        [Fact]
        public void Apply_NoOp_WhenPrefixNullOrWhitespace()
        {
            var pts = new List<GeoPoint> { MakePoint("1", "Name") };
            var count = StationNamePrefixer.Apply(pts, null);
            Assert.Equal(0, count);
            count = StationNamePrefixer.Apply(pts, "   ");
            Assert.Equal(0, count);
        }
    }
}
