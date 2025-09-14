using NUnit.Framework;
using System;
using System.Text;

namespace prepareBikeParking.Tests;

public class GeoPointErrorTests
{
    private string WrapFeatureProps(string propsJson)
    {
        return "\u001e{" + "\"type\":\"FeatureCollection\",\"features\":[{" + "\"type\":\"Feature\",\"properties\":" + propsJson + ",\"geometry\":{\"type\":\"Point\",\"coordinates\":[0,0]}}]}";
    }

    [Test]
    public void MissingAddress_Throws()
    {
    var json = WrapFeatureProps("{\"name\":\"X\",\"latitude\":\"1\",\"longitude\":\"2\"}");
        Assert.Throws<System.Text.Json.JsonException>(() => GeoPoint.ParseLine(json));
    }

    [Test]
    public void MissingLatitude_Throws()
    {
    var json = WrapFeatureProps("{\"address\":\"1\",\"name\":\"X\",\"longitude\":\"2\"}");
        Assert.Throws<System.Text.Json.JsonException>(() => GeoPoint.ParseLine(json));
    }

    [Test]
    public void MissingName_Throws()
    {
    var json = WrapFeatureProps("{\"address\":\"1\",\"latitude\":\"1\",\"longitude\":\"2\"}");
        Assert.Throws<System.Text.Json.JsonException>(() => GeoPoint.ParseLine(json));
    }

    [Test]
    public void EmptyLine_Throws()
    {
        Assert.Throws<ArgumentException>(() => GeoPoint.ParseLine(""));
    }
}
