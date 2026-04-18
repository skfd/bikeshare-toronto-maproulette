using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

namespace prepareBikeParking.Tests;

public class ResilienceTests
{
    [Test]
    public void MissingBikeshareSystemsJson_Throws()
    {
        var path = "src/bikeshare_systems.json";
        var full = Path.Combine("..", "..", "..", path);
        var backup = full+".bak";
        if (File.Exists(full)) File.Move(full, backup);
        try
        {
            Assert.Throws<FileNotFoundException>(() => prepareBikeParking.BikeShareSystemLoader.LoadSystemByIdAsync(1).GetAwaiter().GetResult());
        }
        finally
        {
            if (File.Exists(backup)) File.Move(backup, full);
        }
    }

    [Test]
    public void MissingMaprouletteApiKey_Throws()
    {
        var prev = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
        Environment.SetEnvironmentVariable("MAPROULETTE_API_KEY", null);
        try
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () => await prepareBikeParking.MaprouletteTaskCreator.ValidateProjectAsync(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAPROULETTE_API_KEY", prev);
        }
    }

}
