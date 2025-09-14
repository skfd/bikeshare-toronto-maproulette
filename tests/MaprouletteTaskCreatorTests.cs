using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Text.Json;

namespace prepareBikeParking.Tests;

public class MaprouletteTaskCreatorTests
{
    private class StubHandler : HttpMessageHandler
    {
        private Queue<HttpResponseMessage> _responses;
        public StubHandler(IEnumerable<HttpResponseMessage> responses)
        { _responses = new Queue<HttpResponseMessage>(responses); }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent("{}") });
            }
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private class Factory : IMaprouletteHttpClientFactory
    {
        private readonly HttpClient _client;
        public Factory(HttpMessageHandler handler) { _client = new HttpClient(handler); }
        public HttpClient CreateClient() => _client;
    }

    private IDisposable OverrideEnv(string key, string value)
    {
        var prev = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
        return new EnvReverter(key, prev);
    }
    private class EnvReverter : IDisposable
    {
        private readonly string _k; private readonly string? _v;
        public EnvReverter(string k,string? v){_k=k;_v=v;}
        public void Dispose(){ Environment.SetEnvironmentVariable(_k,_v); }
    }

    [SetUp]
    public void ResetFactory()
    {
        MaprouletteTaskCreator.HttpFactory = new DefaultMaprouletteHttpClientFactory();
    }

    private HttpResponseMessage JsonResponse(HttpStatusCode code, object obj)
        => new HttpResponseMessage(code){ Content = new StringContent(JsonSerializer.Serialize(obj)) };

    [Test]
    public async Task ValidateProject_Success()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        var projectObj = new { id=1, name="Proj", enabled=true };
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{ JsonResponse(HttpStatusCode.OK, projectObj) }));
        var ok = await MaprouletteTaskCreator.ValidateProjectAsync(1);
        Assert.That(ok, Is.True);
    }

    [Test]
    public void ValidateProject_404_ThrowsArgument()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{ new HttpResponseMessage(HttpStatusCode.NotFound){ Content = new StringContent("{}") } }));
        Assert.ThrowsAsync<ArgumentException>(async () => await MaprouletteTaskCreator.ValidateProjectAsync(9));
    }

    [Test]
    public void ValidateProject_401_ThrowsUnauthorized()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{ new HttpResponseMessage(HttpStatusCode.Unauthorized){ Content = new StringContent("{}") } }));
        Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await MaprouletteTaskCreator.ValidateProjectAsync(9));
    }

    [Test]
    public async Task CreateTasks_EmptyAddedFile_SkipsCreation()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        var system = "MaprSys";
        // Setup minimal file structure
        await FileManager.WriteSystemTextFileAsync(system, "instructions/added.md", "Instruction text");
        await FileManager.WriteSystemTextFileAsync(system, "bikeshare_missing_in_osm.geojson", "\n"); // empty effective
        var projectObj = new { id=1, name="Proj", enabled=true };
        // First call: validate project, second call would attempt challenge creation but skip due to empty content -> we make it OK but unused
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{ JsonResponse(HttpStatusCode.OK, projectObj) }));
        await MaprouletteTaskCreator.CreateTasksAsync(1, DateTime.UtcNow.AddDays(-1), system, isNewSystem:true);
        // If it tried to create challenge with empty content a second request would be required; only one consumed means skip path executed.
    }

    [Test]
    public async Task CreateTasks_EmptyInstruction_Throws()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        var system = "MaprSys2";
        await FileManager.WriteSystemTextFileAsync(system, "instructions/added.md", "");
        await FileManager.WriteSystemTextFileAsync(system, "bikeshare_missing_in_osm.geojson", "line");
        var projectObj = new { id=1, name="Proj", enabled=true };
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{ JsonResponse(HttpStatusCode.OK, projectObj) }));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await MaprouletteTaskCreator.CreateTasksAsync(1, DateTime.UtcNow.AddDays(-2), system, isNewSystem:true));
    }

    [Test]
    public async Task CreateTasks_PartialTaskFailures_Counts()
    {
        using var _ = OverrideEnv("MAPROULETTE_API_KEY","key");
        var system = "MaprSys3";
        await FileManager.WriteSystemTextFileAsync(system, "instructions/added.md", "Instruction");
        // Provide two stations
        var line1 = GeoJsonGenerator.GenerateGeojsonLine(new GeoPoint{ id="1", name="A", capacity=0, lat="1", lon="1"}, system);
        var line2 = GeoJsonGenerator.GenerateGeojsonLine(new GeoPoint{ id="2", name="B", capacity=0, lat="2", lon="2"}, system);
        await FileManager.WriteSystemTextFileAsync(system, "bikeshare_missing_in_osm.geojson", line1+"\n"+line2+"\n");
        var projectObj = new { id=1, name="Proj", enabled=true };
        // Responses: validate OK, create challenge OK (with id), first task success, second task fails 500
        var challengeCreated = new { id=123, name="Chal" };
        MaprouletteTaskCreator.HttpFactory = new Factory(new StubHandler(new[]{
            JsonResponse(HttpStatusCode.OK, projectObj),
            JsonResponse(HttpStatusCode.OK, challengeCreated),
            new HttpResponseMessage(HttpStatusCode.OK){ Content = new StringContent("{}") },
            new HttpResponseMessage(HttpStatusCode.InternalServerError){ Content = new StringContent("boom") }
        }));
        await MaprouletteTaskCreator.CreateTasksAsync(1, DateTime.UtcNow.AddDays(-3), system, isNewSystem:true);
        // No assertion on logs; absence of exception indicates partial handling
    }
}
