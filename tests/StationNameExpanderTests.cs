using System.Collections.Generic;
using NUnit.Framework;

namespace prepareBikeParking.Tests
{
    public class StationNameExpanderTests
    {
        private GeoPoint MakePoint(string id, string name) => new GeoPoint
        {
            id = id,
            name = name,
            lat = "0",
            lon = "0"
        };

        [TestCase("28 Ave & 44 St", "28 Avenue & 44 Street")]
        [TestCase("Prospect Ave & E 151 St", "Prospect Avenue & East 151 Street")]
        [TestCase("E 5 St & Ave A", "East 5 Street & Avenue A")]
        [TestCase("Ave C & E 16 St", "Avenue C & East 16 Street")]
        [TestCase("W 35 St & 9 Ave", "West 35 Street & 9 Avenue")]
        [TestCase("Adam Clayton Powell Blvd & W 132 St", "Adam Clayton Powell Boulevard & West 132 Street")]
        [TestCase("Riverside Dr & W 138 St", "Riverside Drive & West 138 Street")]
        [TestCase("Park Circle & East Dr", "Park Circle & East Drive")]
        [TestCase("Vernon Blvd & 41 Rd", "Vernon Boulevard & 41 Road")]
        [TestCase("N 11 St & Kent Ave", "North 11 Street & Kent Avenue")]
        public void ExpandName_HandlesIntersections(string input, string expected)
        {
            Assert.That(StationNameExpander.ExpandName(input), Is.EqualTo(expected));
        }

        [TestCase("St Felix Ave & 61 St", "St Felix Avenue & 61 Street")]
        [TestCase("St Marks Ave & Ralph Ave", "St Marks Avenue & Ralph Avenue")]
        [TestCase("St Nicholas Ave & W 157 St", "St Nicholas Avenue & West 157 Street")]
        [TestCase("W 186 St & St Nicholas Ave", "West 186 Street & St Nicholas Avenue")]
        public void ExpandName_PreservesLeadingSaint(string input, string expected)
        {
            Assert.That(StationNameExpander.ExpandName(input), Is.EqualTo(expected));
        }

        [Test]
        public void ExpandName_PreservesMiddleInitialWithPeriod()
        {
            // "Thomas S. Boyland" — "S." is a middle initial, not the South direction.
            Assert.That(
                StationNameExpander.ExpandName("St Marks Ave & Thomas S. Boyland St"),
                Is.EqualTo("St Marks Avenue & Thomas S. Boyland Street"));
        }

        [TestCase("Crotona Park E", "Crotona Park East")]
        [TestCase("Wilkins Ave & Crotona Park E", "Wilkins Avenue & Crotona Park East")]
        public void ExpandName_ExpandsTrailingDirection(string input, string expected)
        {
            Assert.That(StationNameExpander.ExpandName(input), Is.EqualTo(expected));
        }

        [Test]
        public void ExpandName_HandlesNonIntersectionName()
        {
            Assert.That(StationNameExpander.ExpandName("Broadway"), Is.EqualTo("Broadway"));
            Assert.That(StationNameExpander.ExpandName("South St"), Is.EqualTo("South Street"));
        }

        [TestCase("")]
        [TestCase(null)]
        public void ExpandName_PassesThroughEmpty(string? input)
        {
            Assert.That(StationNameExpander.ExpandName(input!), Is.EqualTo(input));
        }

        [Test]
        public void Apply_NoOp_WhenDisabled()
        {
            var pts = new List<GeoPoint> { MakePoint("1", "28 Ave & 44 St") };
            var changed = StationNameExpander.Apply(pts, enabled: false);
            Assert.That(changed, Is.EqualTo(0));
            Assert.That(pts[0].name, Is.EqualTo("28 Ave & 44 St"));
        }

        [Test]
        public void Apply_CountsOnlyChangedPoints()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", "28 Ave & 44 St"),     // changes
                MakePoint("2", "Broadway"),            // no abbreviations, unchanged
                MakePoint("3", "St Marks Ave & 1 Ave") // changes
            };
            var changed = StationNameExpander.Apply(pts, enabled: true);
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(pts[0].name, Is.EqualTo("28 Avenue & 44 Street"));
            Assert.That(pts[1].name, Is.EqualTo("Broadway"));
            Assert.That(pts[2].name, Is.EqualTo("St Marks Avenue & 1 Avenue"));
        }

        [Test]
        public void Apply_SkipsEmptyNames()
        {
            var pts = new List<GeoPoint>
            {
                MakePoint("1", ""),
                MakePoint("2", "44 St")
            };
            var changed = StationNameExpander.Apply(pts, enabled: true);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(pts[0].name, Is.EqualTo(""));
            Assert.That(pts[1].name, Is.EqualTo("44 Street"));
        }
    }
}
