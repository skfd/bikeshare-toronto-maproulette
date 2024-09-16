// See https://aka.ms/new-console-template for more information
using AngleSharp.Html.Parser;
using prepareBikeParking;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

// Fetch bike share locations
var locationsList = await FetchBikeShareLocations();

// convert each line into this template:
var items = locationsList.OrderBy(x => x.id).Select(generateGeojsonLine);

// join lines and save as geojson file
var geojson = string.Join("\n", items);
File.WriteAllText("../../../bikeshare.geojson", geojson);


// Get the last committed version of the file
string lastCommittedVersion = GitDiffToGeojson.GetLastCommittedVersion();

// Parse the last committed version into a list of GeoPoints
List<GeoPoint> lastCommittedPoints = lastCommittedVersion
    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
    .Select(GeoPoint.ParseLine)
    .ToList();

// Compare the current points with the last committed points
var currentPoints = locationsList;

var addedPoints = currentPoints.ExceptBy(lastCommittedPoints.Select(p => p.id), p => p.id).ToList();
var removedPoints = lastCommittedPoints.ExceptBy(currentPoints.Select(p => p.id), p => p.id).ToList();

var movedOrRenamedPoints = currentPoints
    .Join(lastCommittedPoints,
        current => current.id,
        last => last.id,
        (current, last) => new
        {
            Current = current,
            Last = last,
            HasMoved = current.lat != last.lat || current.lon != last.lon,
            HasRenamed = current.name != last.name
        })
    .Where(p => p.HasMoved || p.HasRenamed)
    .ToList();

var movedPointsFinal = movedOrRenamedPoints
    .Where(p => p.HasMoved)
    .Select(p => p.Current)
    .ToList();

var renamedPoints = movedOrRenamedPoints
    .Where(p => p.HasRenamed && !p.HasMoved)
    .Select(p => p.Current)
    .ToList();

var addedPointsFinal = addedPoints.ToList();
var removedPointsFinal = removedPoints.ToList();





File.WriteAllText("../../../bikeshare_renamed.geojson", string.Join("\n", renamedPoints.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_added.geojson", string.Join("\n", addedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_toreview.geojson", string.Join("\n", addedPoints.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_removed.geojson", string.Join("\n", removedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_moved.geojson", string.Join("\n", movedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));


// Call the function to create the Maproulette task
await CreateMaprouletteRemoveTask(53785);



async Task<List<GeoPoint>> FetchBikeShareLocations()
{
    //download html from url
    var url = "https://bikesharetoronto.com/system-map/";
    var client = new HttpClient();
    var html = await client.GetStringAsync(url);

    //parse html
    var parser = new HtmlParser();
    var document = parser.ParseDocument(html);

    var displaylist = document.GetElementById("infoWind");
    var namesAndCapacities =
        Enumerable.Select(displaylist.Children
    , x => new
    {
        id = x.GetAttribute("id"),
        name = x.Children[0].TextContent.Trim(),
        aproxCapacity = x.Children.Length == 4 ?
                IntParseOrZero(x.Children[1].TextContent) +
                IntParseOrZero(x.Children[3].TextContent) +
                IntParseOrZero(x.Children[2].TextContent) : 0,
    });

    var locations = document.GetElementById("arr_adr").GetAttribute("value");

    //parse json
    var json = JsonSerializer.Deserialize<Dictionary<string, string>>(locations);
    //dictionary with value split by "_" symbol
    var locationsDict = json.ToDictionary(x => x.Key, x => x.Value.Split("_"));
    // dictionary converted to list of objects with id, lat, lon values.
    var locationsList = locationsDict
        .Select(x =>
            new GeoPoint
            {
                id = x.Key,
                name = namesAndCapacities.Single(y => y.id == x.Key).name,
                capacity = namesAndCapacities.Single(y => y.id == x.Key).aproxCapacity,
                lat = x.Value[0],
                lon = x.Value[1]
            })
        .ToList();

    return locationsList;
}

// To get a Maproulette API Key:
// 1. Sign up for a Maproulette account at https://maproulette.org/user/profile
// 2. In the settings page, navigate to the "API KEY" section at the bottom
// 5. Copy the generated API key
// 6. Set the API key as an environment variable named MAPROULETTE_API_KEY
//    - On Windows (PowerShell): [Environment]::SetEnvironmentVariable("MAPROULETTE_API_KEY", "your_api_key_here", "User")
//    - On macOS/Linux: export MAPROULETTE_API_KEY=your_api_key_here
// Note: After setting the environment variable, you may need to restart your terminal or IDE for the changes to take effect

async Task CreateMaprouletteRemoveTask(int projectId)
{
    var client = new HttpClient();
    var apiKey = Environment.GetEnvironmentVariable("MAPROULETTE_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("MAPROULETTE_API_KEY environment variable is not set.");
        return;
    }

    client.DefaultRequestHeaders.Add("apiKey", apiKey);

    var challengeName = $"API TEST: Remove Bikeshare Toronto stations - {DateTime.Now:yyyy-MM-dd}";

    // Create challenge
    var challengeData = new
    {
        name = challengeName,
        description = "Remove Bikeshare Toronto stations that no longer exist",
        instruction = "Please verify and remove the Bikeshare Toronto station from OpenStreetMap.",
        blurb = "Please verify and remove the Bikeshare Toronto station from OpenStreetMap.",
        enabled = true,
        difficulty = 2,
        requiresLocal = false,
        // localGeoJSON = File.ReadAllText("../../../bikeshare_removed.geojson"),
        parent = projectId,

    };

    var challengeResponse = await client.PostAsync(
        "https://maproulette.org/api/v2/challenge",
        new StringContent(JsonSerializer.Serialize(challengeData), Encoding.UTF8, "application/json")
    );

    if (!challengeResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to create challenge: {await challengeResponse.Content.ReadAsStringAsync()}");
        return;
    }

    var challengeResult = JsonSerializer.Deserialize<JsonElement>(await challengeResponse.Content.ReadAsStringAsync());
    var challengeId = challengeResult.GetProperty("id").GetInt32();

    var values = File.ReadAllText("../../../bikeshare_removed.geojson");
    var taskResponse = await client.PostAsync(
        $"https://maproulette.org/api/v2/challenge/{challengeId}/addTasks",
        new StringContent(values, Encoding.UTF8, "application/json")
    );

    if (!taskResponse.IsSuccessStatusCode)
    {
        Console.WriteLine($"Failed to create task: {await taskResponse.Content.ReadAsStringAsync()}");
    }

    Console.WriteLine($"Maproulette challenge created: {challengeName} (ID: {challengeId})");
}





static string generateGeojsonLine(GeoPoint x)
{
    var template = "\u001e{{\"type\":\"FeatureCollection\"" +
        ",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\"," +
        "\"coordinates\":[{0},{1}]}},\"properties\":{{" +
                "\"address\":\"{2}\"," +
                "\"latitude\":\"{1}\"," +
                "\"longitude\":\"{0}\"," +
                "\"name\":\"{3}\"," +
                "\"capacity\":\"{4}\"," +
        "\"operator\":\"{5}\"}}}}]}}";

    return string.Format(template, x.lon, x.lat, x.id, x.name, x.capacity, "BikeShare Toronto");
}

static int IntParseOrZero(string inp)
{
    var yes = int.TryParse(Regex.Match(inp, @"\d+").Value, out var result);

    return yes ? result : 0;
}


//amenity = bicycle_rental
//bicycle_rental = docking_station
//brand = Bike Share Toronto
//brand:wikidata = Q17018523
//brand: wikipedia = en:Bike Share Toronto
//name=Bike Share Toronto
//operator=Toronto Parking Authority
//operator:wikidata = Q7826466
//operator:wikipedia= en:Toronto Parking Authority
