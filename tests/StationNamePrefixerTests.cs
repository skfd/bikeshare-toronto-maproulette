using System.Collections.Generic;
using NUnit.Framework;

namespace prepareBikeParking.Tests
{
    public class StationNamePrefixerTests
    {
        private GeoPoint MakePoint(string id, string name) => new GeoPoint
        {
            id = id,
            name = name,
            lat = "0",
            lon = "0"
        };

        [Test]
        public void Apply_AddsPrefix_WhenMissing()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", "Alpha"),
                MakePoint("2", "Beta")
            };
            var count = StationNamePrefixer.Apply(pts, "Pre - ");
            Assert.That(count, Is.EqualTo(2));
            foreach (var p in pts)
            {
                Assert.That(p.name, Does.StartWith("Pre - "));
            }
        }

        [Test]
        public void Apply_Skips_WhenAlreadyPrefixed_IgnoringCase()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", "Pre - Alpha"),
                MakePoint("2", "pre - Beta")
            };
            var count = StationNamePrefixer.Apply(pts, "Pre - ");
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void Apply_NoOp_WhenPrefixNullOrWhitespace()
        {
            var pts = new List<GeoPoint> { MakePoint("1", "Name") };
            var count = StationNamePrefixer.Apply(pts, null);
            Assert.That(count, Is.EqualTo(0));
            count = StationNamePrefixer.Apply(pts, "   ");
            Assert.That(count, Is.EqualTo(0));
        }
    }
}
