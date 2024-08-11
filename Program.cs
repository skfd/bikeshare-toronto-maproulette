// See https://aka.ms/new-console-template for more information
using AngleSharp.Html.Parser;
using prepareBikeParking;
using System.Text.Json;


//dowload html from url
var url = "https://bikesharetoronto.com/system-map/";
var client = new HttpClient();
var html = await client.GetStringAsync(url);

//parse html
var parser = new HtmlParser();
var document = parser.ParseDocument(html);

//get element by id icon_txt
var bikes = document.GetElementById("icon_txt");
var ebikes = document.GetElementById("e_icon_txt");
var arr_adr = document.GetElementById("arr_adr");

var locations = arr_adr.GetAttribute("value");
//parse json
var json = JsonSerializer.Deserialize<Dictionary<string, string>>(locations);
//dictionary with value split by "_" symbol
var locationsDict = json.ToDictionary(x => x.Key, x => x.Value.Split("_"));
// dictionary converted to list of objects with id, lat, lon values.
var locationsList = locationsDict.Select(x => new GeoPoint { id = x.Key, lat = x.Value[0], lon = x.Value[1] }).ToList();

// convert each line into this template:
var items = locationsList.OrderBy(x => x.id).Select(generateGeojsonLine);

// join lines and save as geojson file
var geojson = string.Join("\n", items);
File.WriteAllText("../../../bikeshare.geojson", geojson);



var (addedlines, removedObjects) = GitDiffToGeojson.Compare("HEAD");

var addedPoints = addedlines.Select(GeoPoint.ParseLine).ToList();

var removedPoints = removedObjects.Select(GeoPoint.ParseLine).ToList();


var addedIds = addedPoints.Select(x => x.id).ToList();
var removedIds = removedPoints.Select(x => x.id).ToList();
var movedIds = addedIds.Intersect(removedIds).ToList();

var movedPointsFinal = addedPoints.Where(x => movedIds.Contains(x.id)).ToList();
var addedPointsFinal = addedPoints.Where(x => !movedIds.Contains(x.id)).ToList();
var removedPointsFinal = removedPoints.Where(x => !movedIds.Contains(x.id)).ToList();



File.WriteAllText("../../../bikeshare_added.geojson", string.Join("\n", addedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_toreview.geojson", string.Join("\n", addedPoints.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_removed.geojson", string.Join("\n", removedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));
File.WriteAllText("../../../bikeshare_moved.geojson", string.Join("\n", movedPointsFinal.OrderBy(x => x.id).Select(generateGeojsonLine)));

static string generateGeojsonLine(GeoPoint x)
{
    var template = "\u001e{{\"type\":\"FeatureCollection\",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"Point\",\"coordinates\":[{0},{1}]}},\"properties\":{{\"address\":\"{2}\",\"latitude\":\"{1}\",\"longitude\":\"{0}\",\"operator\":\"{3}\"}}}}]}}";

    return string.Format(template, x.lon, x.lat, x.id, "BikeShare Toronto");
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
