using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace prepareBikeParking.Tests;

public class RunReportTests
{
    private const string TempSystem = "TestSystem_RunReport";

    [SetUp]
    [TearDown]
    public void Clean()
    {
        var full = FileManager.GetSystemFullPath(TempSystem, "");
        if (Directory.Exists(full)) Directory.Delete(full, true);
    }

    private static RunReport.Result SampleResult() => new(
        TempSystem,
        "Testville",
        new DateTime(2026, 9, 9, 11, 0, 0, DateTimeKind.Utc),
        Succeeded: true,
        Error: null,
        new Dictionary<string, int> { ["Missing in OSM"] = 2 },
        new List<string> { "Complete MapRoulette tasks for 2 station(s) missing in OSM." });

    [Test]
    public void ReportFiles_HaveNoByteOrderMark()
    {
        // The weekly workflow concatenates every system's last_run.md into one
        // issue body. A BOM in front of "# System" for any file but the first
        // makes GitHub render a literal '#' instead of a heading.
        RunReport.Write(SampleResult());

        foreach (var name in new[] { "last_run.md", "last_run.json" })
        {
            var bytes = File.ReadAllBytes(FileManager.GetSystemFullPath(TempSystem, name));
            Assert.That(bytes.Length, Is.GreaterThan(3), name);
            Assert.That(bytes[0..3], Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), $"{name} starts with a UTF-8 BOM");
        }
    }

    [Test]
    public void Markdown_StartsWithHeading_AndKeepsUtf8Text()
    {
        RunReport.Write(SampleResult() with { City = "Montréal" });

        var md = File.ReadAllText(FileManager.GetSystemFullPath(TempSystem, "last_run.md"), new UTF8Encoding(false));
        Assert.That(md, Does.StartWith($"# {TempSystem} (Montréal)"));
        Assert.That(md, Does.Contain("## Next steps"));
        Assert.That(md, Does.Contain("- [ ] Complete MapRoulette tasks"));
    }
}
