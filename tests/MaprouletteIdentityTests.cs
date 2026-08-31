using NUnit.Framework;
using System.Text.Json;

namespace prepareBikeParking.Tests;

/// <summary>
/// Task identity: the join between a MapRoulette task and the station it is about.
/// Without a stable key a refresh cannot tell an existing task from a new one, and
/// every run duplicates the whole challenge.
/// </summary>
public class MaprouletteIdentityTests
{
    private const string Rs = "";

    private static string Line(string properties) =>
        "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\"," +
        "\"geometry\":{\"type\":\"Point\",\"coordinates\":[-79.4,43.65]}," +
        "\"properties\":{" + properties + "}}]}";

    [Test]
    public void GbfsStationIdIsTheKeyWhenThereIsNoOsmObject()
    {
        var key = MaprouletteSync.TaskKey(Line("\"address\":\"7042\",\"name\":\"Bay St\""));
        Assert.That(key, Is.EqualTo("7042"));
    }

    [Test]
    public void OsmObjectIdentifiesItself()
    {
        // Duplicate-ref tasks need this: refs repeat there by definition, so the
        // ref alone would not be unique within the challenge.
        var key = MaprouletteSync.TaskKey(
            Line("\"address\":\"7042\",\"osmType\":\"node\",\"osmId\":\"123456\""));
        Assert.That(key, Is.EqualTo("node/123456"));
    }

    [Test]
    public void StationWithNoRefFallsBackToItsPositionRatherThanBeingDropped()
    {
        var key = MaprouletteSync.TaskKey(
            Line("\"address\":\"\",\"latitude\":\"43.65\",\"longitude\":\"-79.4\""));
        Assert.That(key, Is.EqualTo("@43.65,-79.4"));
    }

    [Test]
    public void UnparseableLineYieldsNoKey()
    {
        Assert.That(MaprouletteSync.TaskKey("not json at all"), Is.Null);
    }

    [Test]
    public void UploadStampsTheStableKeyOntoTheFeature()
    {
        var body = MaprouletteApi.WithStableId(Line("\"address\":\"7042\""), "7042");

        var id = JsonDocument.Parse(body).RootElement
            .GetProperty("features")[0]
            .GetProperty("properties")
            .GetProperty("@id").GetString();

        Assert.That(id, Is.EqualTo("7042"), "MapRoulette names the task after @id");
    }

    [Test]
    public void UploadPreservesTheOriginalProperties()
    {
        var body = MaprouletteApi.WithStableId(Line("\"address\":\"7042\",\"name\":\"Bay St\""), "7042");
        var props = JsonDocument.Parse(body).RootElement
            .GetProperty("features")[0].GetProperty("properties");

        Assert.That(props.GetProperty("name").GetString(), Is.EqualTo("Bay St"));
        Assert.That(props.GetProperty("address").GetString(), Is.EqualTo("7042"));
    }

    [Test]
    public void RecordSeparatorIsNotPartOfTheUploadedBody()
    {
        // addTasks rejects anything that is not a single JSON value, so the
        // record separator our files are delimited with has to come off first.
        var separated = Rs + Line("\"address\":\"7042\"");
        var body = MaprouletteApi.WithStableId(separated.TrimStart(Rs[0]), "7042");

        Assert.That(body[0], Is.EqualTo('{'), "body must start with the JSON object");
        Assert.That(body.IndexOf(Rs[0]), Is.EqualTo(-1), "no record separator anywhere in the body");
        Assert.That(JsonDocument.Parse(body), Is.Not.Null);
    }
}
