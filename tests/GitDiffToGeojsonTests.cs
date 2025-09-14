using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace prepareBikeParking.Tests;

public class GitDiffToGeojsonTests
{
    [Test]
    public void ExtractDiffedLines_AddedRemovedParsed()
    {
        var input = "+\u001e{\"id\":\"1\"}\n-\u001e{\"id\":\"2\"}\n";
        var (added, removed) = prepareBikeParking.GitDiffToGeojson.ExtractDiffedLines(input);
        Assert.That(added.Count, Is.EqualTo(1));
        Assert.That(removed.Count, Is.EqualTo(1));
        Assert.That(added[0], Is.EqualTo("{\"id\":\"1\"}"));
        Assert.That(removed[0], Is.EqualTo("{\"id\":\"2\"}"));
    }

    [Test]
    public void ExtractDiffedLines_EmptyInput()
    {
        var (added, removed) = prepareBikeParking.GitDiffToGeojson.ExtractDiffedLines("");
        Assert.That(added, Is.Empty);
        Assert.That(removed, Is.Empty);
    }

    [Test]
    public void GetLastCommittedVersion_UntrackedFile_Throws()
    {
        Assert.Throws<System.IO.FileNotFoundException>(() => prepareBikeParking.GitDiffToGeojson.GetLastCommittedVersion("not-a-file.geojson"));
    }
}
