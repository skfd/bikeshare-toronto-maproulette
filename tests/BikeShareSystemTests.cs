using NUnit.Framework;

namespace prepareBikeParking.Tests
{
    public class BikeShareSystemTests
    {
        // Covers the real URL shapes in src/bikeshare_systems.json across GBFS v1, v2.2, v2.3 and legacy SoBi.
        [TestCase("https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_information.json",
                  "https://tor.publicbikesystem.net/ube/gbfs/v1/en/station_status.json")]
        [TestCase("https://gbfs.velobixi.com/gbfs/2-2/en/station_information.json",
                  "https://gbfs.velobixi.com/gbfs/2-2/en/station_status.json")]
        [TestCase("https://quebec.publicbikesystem.net/customer/ube/gbfs/v1/en/station_information",
                  "https://quebec.publicbikesystem.net/customer/ube/gbfs/v1/en/station_status")]
        [TestCase("https://gbfs.lyft.com/gbfs/2.3/bkn/en/station_information.json",
                  "https://gbfs.lyft.com/gbfs/2.3/bkn/en/station_status.json")]
        [TestCase("https://gbfs.kappa.fifteen.eu/gbfs/2.2/mobi/en/station_information.json",
                  "https://gbfs.kappa.fifteen.eu/gbfs/2.2/mobi/en/station_status.json")]
        [TestCase("https://hamilton.socialbicycles.com/opendata/station_information.json",
                  "https://hamilton.socialbicycles.com/opendata/station_status.json")]
        public void GetStationStatusUrl_SwapsLastSegment(string gbfsApi, string expected)
        {
            var system = new BikeShareSystem { GbfsApi = gbfsApi };
            Assert.That(system.GetStationStatusUrl(), Is.EqualTo(expected));
        }

        [Test]
        public void GetStationStatusUrl_UnrecognizedLastSegment_ReturnsUnchanged()
        {
            var system = new BikeShareSystem { GbfsApi = "https://example.com/gbfs/some_other_feed.json" };
            Assert.That(system.GetStationStatusUrl(), Is.EqualTo("https://example.com/gbfs/some_other_feed.json"));
        }

        [Test]
        public void GetStationStatusUrl_Empty_ReturnsAsIs()
        {
            var system = new BikeShareSystem { GbfsApi = "" };
            Assert.That(system.GetStationStatusUrl(), Is.EqualTo(""));
        }
    }
}
