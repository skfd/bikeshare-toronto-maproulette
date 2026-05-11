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
    public void MissingGbfsSystemId_Throws()
    {
        // Write a temp config in the working dir (one of the loader's candidate search paths).
        var tempConfig = "bikeshare_systems.json";
        var hadExisting = File.Exists(tempConfig);
        var backup = tempConfig + ".bak";
        if (hadExisting) File.Move(tempConfig, backup, overwrite: true);
        try
        {
            File.WriteAllText(tempConfig, "[{\"id\":99,\"name\":\"Broken\",\"city\":\"Nowhere\",\"gbfs_api\":\"https://example.com/x.json\"}]");
            var ex = Assert.Throws<InvalidOperationException>(() => prepareBikeParking.BikeShareSystemLoader.LoadAllSystemsAsync().GetAwaiter().GetResult());
            Assert.That(ex!.Message, Does.Contain("gbfs_system_id"));
            Assert.That(ex.Message, Does.Contain("Broken"));
        }
        finally
        {
            if (File.Exists(tempConfig)) File.Delete(tempConfig);
            if (hadExisting && File.Exists(backup)) File.Move(backup, tempConfig, overwrite: true);
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
