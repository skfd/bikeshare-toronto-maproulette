using NUnit.Framework;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using prepareBikeParking.Services;

namespace prepareBikeParking.Tests;

public class BikeShareFlowsTests
{
    private GeoPoint Pt(string id, string name) => new GeoPoint{ id=id, name=name, capacity=0, lat="43.0", lon="-79.0"};

    [Test]
    public async Task RunSystemFlow_ExistingSystem_GeneratesMainDiffAndOsmCompare_NoTasksWhenDeclined()
    {
        var system = new BikeShareSystem { Id=1, Name="TestSys", City="CityX", GbfsApi="https://example", MaprouletteProjectId=123 };
        var currentPoints = new List<GeoPoint>{ Pt("1","Old"), Pt("2","New") };
        var previousPoints = new List<GeoPoint>{ Pt("1","Old") };
        // Generate previous version content (one line)
        var prevLine = prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLine(previousPoints[0], system.Name);
        var prevContent = prevLine + "\n";

        var loader = new Mock<IBikeShareSystemLoader>();
        loader.Setup(l => l.LoadByIdAsync(system.Id)).ReturnsAsync(system);

        var fetcher = new Mock<IBikeShareDataFetcher>();
        fetcher.Setup(f => f.FetchStationsAsync(It.IsAny<string>())).ReturnsAsync(currentPoints);

        var git = new Mock<IGitReader>();
        git.Setup(g => g.GetLastCommitDate(It.IsAny<string>())).Returns(DateTime.UtcNow.AddDays(-1));
        git.Setup(g => g.GetLastCommittedVersion(It.IsAny<string>())).Returns(prevContent);

        var comparer = new Mock<IComparerService>();
        // First compare (moveThreshold 3) returns one added (id 2)
        comparer.Setup(c => c.Compare(currentPoints, It.IsAny<List<GeoPoint>>(), 3))
            .Returns((new List<GeoPoint>{ currentPoints[1] }, new List<GeoPoint>(), new List<GeoPoint>(), new List<(GeoPoint current, GeoPoint old)>()));
        // Second compare (moveThreshold 30) returns empty differences
        comparer.Setup(c => c.Compare(currentPoints, It.IsAny<List<GeoPoint>>(), 30))
            .Returns((new List<GeoPoint>(), new List<GeoPoint>(), new List<GeoPoint>(), new List<(GeoPoint current, GeoPoint old)>()));

        var geoWriter = new Mock<IGeoJsonWriter>();
        geoWriter.Setup(w => w.WriteMainAsync(currentPoints, system.Name)).Returns(Task.CompletedTask).Verifiable();
        geoWriter.Setup(w => w.WriteDiffAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Returns(Task.CompletedTask)
            .Callback<List<GeoPoint>, List<GeoPoint>, List<GeoPoint>, List<(GeoPoint current, GeoPoint old)>, string>((added, removed, moved, renamed, s) => {
                Assert.That(added.Count, Is.EqualTo(1));
                Assert.That(added[0].id, Is.EqualTo("2"));
            })
            .Verifiable();
        geoWriter.Setup(w => w.WriteOsmCompareAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Returns(Task.CompletedTask).Verifiable();

        var osmFetcher = new Mock<IOSMDataFetcher>();
        osmFetcher.Setup(o => o.EnsureStationsFileAsync(system.Name, system.City)).Returns(Task.CompletedTask);
        osmFetcher.Setup(o => o.FetchOsmStationsAsync(system.Name, system.City)).ReturnsAsync(new List<GeoPoint>());

        var osmChangeWriter = new Mock<IOsmChangeWriter>();
        osmChangeWriter.Setup(o => o.WriteRenameChangesAsync(It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name)).Returns(Task.CompletedTask).Verifiable();

        var maproulette = new Mock<IMaprouletteService>();
        maproulette.Setup(m => m.ValidateProjectAsync(system.MaprouletteProjectId)).ReturnsAsync(true);
        maproulette.Setup(m => m.CreateTasksAsync(It.IsAny<int>(), It.IsAny<DateTime>(), system.Name, It.IsAny<bool>()))
            .Throws(new Exception("Should not be called when user declines"));

        var setupSvc = new Mock<ISystemSetupService>();
        setupSvc.Setup(s => s.ValidateSystem(system.Name, false)).Returns(new SystemValidationResult{ SystemName=system.Name, IsValid=true });
    setupSvc.Setup(s => s.EnsureAsync(system.Name, system.Name, system.Name, system.City)).ReturnsAsync(false);
        setupSvc.Setup(s => s.ValidateInstructionFiles(system.Name)).Verifiable();

        var paths = new Mock<IFilePathProvider>();
        paths.Setup(p => p.GetSystemFullPath(system.Name, "bikeshare.geojson")).Returns("dummy.geojson");

        var prompt = new Mock<IPromptService>();
        prompt.Setup(p => p.ReadConfirmation(It.IsAny<string>(), 'n')).Returns('n');

        var flows = new BikeShareFlows(fetcher.Object, osmFetcher.Object, geoWriter.Object, comparer.Object, git.Object, maproulette.Object, setupSvc.Object, paths.Object, prompt.Object, loader.Object, osmChangeWriter.Object);
        await flows.RunSystemFlow(system.Id);

        geoWriter.Verify();
        osmFetcher.Verify(o => o.FetchOsmStationsAsync(system.Name, system.City), Times.Once);
        maproulette.Verify(m => m.CreateTasksAsync(It.IsAny<int>(), It.IsAny<DateTime>(), system.Name, It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task RunSystemFlow_NewSystem_AllStationsAddedWhenNoGitHistory()
    {
        var system = new BikeShareSystem { Id=2, Name="NewSys", City="CityY", GbfsApi="https://example", MaprouletteProjectId=0 };
        var currentPoints = new List<GeoPoint>{ Pt("10","Only") };

        var loader = new Mock<IBikeShareSystemLoader>();
        loader.Setup(l => l.LoadByIdAsync(system.Id)).ReturnsAsync(system);

        var fetcher = new Mock<IBikeShareDataFetcher>();
        fetcher.Setup(f => f.FetchStationsAsync(It.IsAny<string>())).ReturnsAsync(currentPoints);

        var git = new Mock<IGitReader>();
        git.Setup(g => g.GetLastCommitDate(It.IsAny<string>())).Returns((DateTime?)null); // new system
        git.Setup(g => g.GetLastCommittedVersion(It.IsAny<string>())).Throws(new FileNotFoundException("not found in git repository"));

        var comparer = new Mock<IComparerService>();

        var geoWriter = new Mock<IGeoJsonWriter>();
        geoWriter.Setup(w => w.WriteMainAsync(currentPoints, system.Name)).Returns(Task.CompletedTask);
        geoWriter.Setup(w => w.WriteDiffAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Callback<List<GeoPoint>, List<GeoPoint>, List<GeoPoint>, List<(GeoPoint current, GeoPoint old)>, string>((added, removed, moved, renamed, s) => {
                Assert.That(added.Count, Is.EqualTo(1));
                Assert.That(removed, Is.Empty);
            })
            .Returns(Task.CompletedTask).Verifiable();
        geoWriter.Setup(w => w.WriteOsmCompareAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Returns(Task.CompletedTask);

        var osmFetcher = new Mock<IOSMDataFetcher>();
        osmFetcher.Setup(o => o.EnsureStationsFileAsync(system.Name, system.City)).Returns(Task.CompletedTask);
        osmFetcher.Setup(o => o.FetchOsmStationsAsync(system.Name, system.City)).ReturnsAsync(new List<GeoPoint>());

        var osmChangeWriter = new Mock<IOsmChangeWriter>();
        osmChangeWriter.Setup(o => o.WriteRenameChangesAsync(It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name)).Returns(Task.CompletedTask);

        var maproulette = new Mock<IMaprouletteService>();
        maproulette.Setup(m => m.ValidateProjectAsync(It.IsAny<int>())).ReturnsAsync(true);

        var setupSvc = new Mock<ISystemSetupService>();
        setupSvc.Setup(s => s.ValidateSystem(system.Name, false)).Returns(new SystemValidationResult{ SystemName=system.Name, IsValid=true });
    setupSvc.Setup(s => s.EnsureAsync(system.Name, system.Name, system.Name, system.City)).ReturnsAsync(false);

        var paths = new Mock<IFilePathProvider>();
        paths.Setup(p => p.GetSystemFullPath(system.Name, "bikeshare.geojson")).Returns("dummy-new.geojson");

        var prompt = new Mock<IPromptService>();
        prompt.Setup(p => p.ReadConfirmation(It.IsAny<string>(), 'n')).Returns('n');

        var flows = new BikeShareFlows(fetcher.Object, osmFetcher.Object, geoWriter.Object, comparer.Object, git.Object, maproulette.Object, setupSvc.Object, paths.Object, prompt.Object, loader.Object, osmChangeWriter.Object);
        await flows.RunSystemFlow(system.Id);

        // Ensure comparer not used for first diff (new system path short-circuits)
        comparer.Verify(c => c.Compare(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), 3), Times.Never);
        geoWriter.Verify();
    }

    [Test]
    public async Task RunSystemFlow_ProjectValidationFails_TasksNotCreated()
    {
        var system = new BikeShareSystem { Id=3, Name="ValFail", City="CityZ", GbfsApi="https://example", MaprouletteProjectId=999 };
        var currentPoints = new List<GeoPoint>{ Pt("1","A") };

        var loader = new Mock<IBikeShareSystemLoader>();
        loader.Setup(l => l.LoadByIdAsync(system.Id)).ReturnsAsync(system);

        var fetcher = new Mock<IBikeShareDataFetcher>();
        fetcher.Setup(f => f.FetchStationsAsync(It.IsAny<string>())).ReturnsAsync(currentPoints);

        var git = new Mock<IGitReader>();
        git.Setup(g => g.GetLastCommitDate(It.IsAny<string>())).Returns(DateTime.UtcNow.AddDays(-2));
        git.Setup(g => g.GetLastCommittedVersion(It.IsAny<string>())).Returns(prepareBikeParking.GeoJsonGenerator.GenerateGeojsonLine(currentPoints[0], system.Name)+"\n");

        var comparer = new Mock<IComparerService>();
        comparer.Setup(c => c.Compare(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), 3))
            .Returns((new List<GeoPoint>(), new List<GeoPoint>(), new List<GeoPoint>(), new List<(GeoPoint current, GeoPoint old)>()));
        comparer.Setup(c => c.Compare(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), 30))
            .Returns((new List<GeoPoint>(), new List<GeoPoint>(), new List<GeoPoint>(), new List<(GeoPoint current, GeoPoint old)>()));

        var geoWriter = new Mock<IGeoJsonWriter>();
        geoWriter.Setup(w => w.WriteMainAsync(currentPoints, system.Name)).Returns(Task.CompletedTask).Verifiable();
        geoWriter.Setup(w => w.WriteDiffAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Returns(Task.CompletedTask);
        geoWriter.Setup(w => w.WriteOsmCompareAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name))
            .Returns(Task.CompletedTask);

        var osmFetcher = new Mock<IOSMDataFetcher>();
        osmFetcher.Setup(o => o.EnsureStationsFileAsync(system.Name, system.City)).Returns(Task.CompletedTask);
        osmFetcher.Setup(o => o.FetchOsmStationsAsync(system.Name, system.City)).ReturnsAsync(new List<GeoPoint>());

        var osmChangeWriter = new Mock<IOsmChangeWriter>();
        osmChangeWriter.Setup(o => o.WriteRenameChangesAsync(It.IsAny<List<(GeoPoint current, GeoPoint old)>>(), system.Name)).Returns(Task.CompletedTask);

        var maproulette = new Mock<IMaprouletteService>();
        maproulette.Setup(m => m.ValidateProjectAsync(system.MaprouletteProjectId)).ReturnsAsync(false); // validation fails
        maproulette.Setup(m => m.CreateTasksAsync(It.IsAny<int>(), It.IsAny<DateTime>(), system.Name, It.IsAny<bool>()))
            .Throws(new Exception("Should not create tasks on validation failure"));

        var setupSvc = new Mock<ISystemSetupService>();
        setupSvc.Setup(s => s.ValidateSystem(system.Name, false)).Returns(new SystemValidationResult{ SystemName=system.Name, IsValid=true });
    setupSvc.Setup(s => s.EnsureAsync(system.Name, system.Name, system.Name, system.City)).ReturnsAsync(false);
        setupSvc.Setup(s => s.ValidateInstructionFiles(system.Name)).Verifiable();

        var paths = new Mock<IFilePathProvider>();
        paths.Setup(p => p.GetSystemFullPath(system.Name, "bikeshare.geojson")).Returns("dummy-valfail.geojson");

        var prompt = new Mock<IPromptService>();
    prompt.Setup(p => p.ReadConfirmation(It.IsAny<string>(), 'n')).Returns('n'); // decline task creation

        var flows = new BikeShareFlows(fetcher.Object, osmFetcher.Object, geoWriter.Object, comparer.Object, git.Object, maproulette.Object, setupSvc.Object, paths.Object, prompt.Object, loader.Object, osmChangeWriter.Object);
        await flows.RunSystemFlow(system.Id);
        // Should not attempt task creation, but still write main output
        maproulette.Verify(m => m.CreateTasksAsync(It.IsAny<int>(), It.IsAny<DateTime>(), system.Name, It.IsAny<bool>()), Times.Never);
        geoWriter.Verify(w => w.WriteMainAsync(currentPoints, system.Name), Times.Once);
    }

    [Test]
    public async Task RunSystemFlow_NewScaffold_EarlyExitBeforeFetching()
    {
        var system = new BikeShareSystem { Id=4, Name="ScaffoldOnly", City="CityS", GbfsApi="https://example", MaprouletteProjectId=0 };

        var loader = new Mock<IBikeShareSystemLoader>();
        loader.Setup(l => l.LoadByIdAsync(system.Id)).ReturnsAsync(system);

        var fetcher = new Mock<IBikeShareDataFetcher>();
        fetcher.Setup(f => f.FetchStationsAsync(It.IsAny<string>())).Throws(new Exception("Should not fetch when scaffolding"));

        var git = new Mock<IGitReader>();
        var comparer = new Mock<IComparerService>();
        var geoWriter = new Mock<IGeoJsonWriter>();
        var osmFetcher = new Mock<IOSMDataFetcher>();
        var osmChangeWriter = new Mock<IOsmChangeWriter>();
        var maproulette = new Mock<IMaprouletteService>();
        maproulette.Setup(m => m.ValidateProjectAsync(It.IsAny<int>())).ReturnsAsync(true);

        var setupSvc = new Mock<ISystemSetupService>();
        setupSvc.Setup(s => s.ValidateSystem(system.Name, false)).Returns(new SystemValidationResult{ SystemName=system.Name, IsValid=false });
        // Return true meaning newly created
        setupSvc.Setup(s => s.EnsureAsync(system.Name, system.Name, system.Name, system.City)).ReturnsAsync(true);

        var paths = new Mock<IFilePathProvider>();
        paths.Setup(p => p.GetSystemFullPath(system.Name, "bikeshare.geojson")).Returns("dummy.geojson");
        var prompt = new Mock<IPromptService>();
        prompt.Setup(p => p.ReadConfirmation(It.IsAny<string>(), 'n')).Returns('n');

        var flows = new BikeShareFlows(fetcher.Object, osmFetcher.Object, geoWriter.Object, comparer.Object, git.Object, maproulette.Object, setupSvc.Object, paths.Object, prompt.Object, loader.Object, osmChangeWriter.Object);
        await flows.RunSystemFlow(system.Id);

        // Ensure no downstream calls executed
        geoWriter.Verify(g => g.WriteMainAsync(It.IsAny<List<GeoPoint>>(), It.IsAny<string>()), Times.Never);
        fetcher.Verify(f => f.FetchStationsAsync(It.IsAny<string>()), Times.Never);
        comparer.Verify(c => c.Compare(It.IsAny<List<GeoPoint>>(), It.IsAny<List<GeoPoint>>(), It.IsAny<double>()), Times.Never);
    }
}
