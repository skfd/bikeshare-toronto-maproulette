using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using System;

namespace prepareBikeParking.Tests;

public class SystemSetupHelperTests
{
    private string _system = "TestSystem_Setup";

    private string SystemPath => prepareBikeParking.FileManager.GetSystemFullPath(_system, "");
    private string InstructionsPath => prepareBikeParking.FileManager.GetSystemFullPath(_system, "instructions");

    [SetUp]
    public void Clean()
    {
        if (Directory.Exists(SystemPath)) Directory.Delete(SystemPath, true);
    }

    [Test]
    public async Task SetupNewSystem_CreatesInstructionFilesAndOverpass()
    {
        await prepareBikeParking.SystemSetupHelper.SetupNewSystemAsync(_system, "OpName", "BrandName", operatorType: "public", cityName: "CityX");
        Assert.That(Directory.Exists(SystemPath), Is.True);
        Assert.That(File.Exists(Path.Combine(InstructionsPath, "added.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(InstructionsPath, "removed.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(InstructionsPath, "moved.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(InstructionsPath, "renamed.md")), Is.True);
        Assert.That(File.Exists(prepareBikeParking.FileManager.GetSystemFullPath(_system, "stations.overpass")), Is.True);
    }

    [Test]
    public void ValidateSystemSetup_MissingFilesReturnsInvalid()
    {
        // Create only system directory without instructions
        Directory.CreateDirectory(SystemPath);
        var validation = prepareBikeParking.SystemSetupHelper.ValidateSystemSetup(_system, throwOnMissing: false);
        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.MissingFiles.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ValidateInstructionFilesForTaskCreation_ThrowsWhenMissing()
    {
        Directory.CreateDirectory(SystemPath);
        // No instruction files -> should throw
        Assert.Throws<InvalidOperationException>(() => prepareBikeParking.SystemSetupHelper.ValidateInstructionFilesForTaskCreation(_system));
    }

    [Test]
    public void ValidateInstructionFilesForTaskCreation_ThrowsWhenEmpty()
    {
        Directory.CreateDirectory(SystemPath);
        Directory.CreateDirectory(InstructionsPath);
        // Create required files but leave them empty
        File.WriteAllText(Path.Combine(InstructionsPath, "added.md"), string.Empty);
        File.WriteAllText(Path.Combine(InstructionsPath, "removed.md"), string.Empty);
        File.WriteAllText(Path.Combine(InstructionsPath, "moved.md"), string.Empty);
        // renamed not required for task creation
        Assert.Throws<InvalidOperationException>(() => prepareBikeParking.SystemSetupHelper.ValidateInstructionFilesForTaskCreation(_system));
    }

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(SystemPath)) Directory.Delete(SystemPath, true);
    }
}
