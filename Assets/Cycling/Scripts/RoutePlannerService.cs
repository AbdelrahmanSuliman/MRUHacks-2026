using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace CalgaryCycling
{
    // OpenRouteService transport seam. The desktop planner can use this service
    // for Calgary geocoding and cycling routes without storing the API key in assets.
    public sealed class RoutePlannerService : MonoBehaviour
    {
        [Serializable] sealed class Secrets { public string openRouteServiceApiKey; }
        [Serializable] sealed class PlaceCollection { public PlaceFeature[] features; }
        [Serializable] sealed class PlaceFeature { public PlaceGeometry geometry; public PlaceProperties properties; }
        [Serializable] sealed class PlaceGeometry { public double[] coordinates; }
        [Serializable] sealed class PlaceProperties { public string label; }
        public sealed class Place { public string label; public double longitude, latitude; }
        public sealed class RouteResult { public readonly List<Place> points = new List<Place>(); public float distance; public float duration; }
        public event Action<string> Failed;
        public event Action<List<Place>> PlacesFound;
        public event Action<RouteResult> RouteFound;

        string apiKey;
        bool loadingStreamingKey;
        void Awake() { LoadKey(); if(!UsableKey(apiKey))StartCoroutine(LoadStreamingKey()); }
        void LoadKey()
        {
            apiKey=Environment.GetEnvironmentVariable("CALGARY_ORS_API_KEY");
            if(UsableKey(apiKey))return;
            var paths=new List<string>
            {
                Path.Combine(Application.persistentDataPath,"cycling-secrets.json")
            };
#if UNITY_EDITOR
            paths.Add(Path.GetFullPath(Path.Combine(Application.dataPath,"..","cycling-secrets.json")));
#endif
            for(int i=0;i<paths.Count;i++)
            {
                try
                {
                    if(!File.Exists(paths[i]))continue;
                    apiKey=ParseKey(File.ReadAllText(paths[i]));
                    if(UsableKey(apiKey))return;
                }
                catch(Exception ex){Debug.LogWarning("[RoutePlannerService] Could not read route credentials: "+ex.Message);}
            }
            apiKey=null;
        }
        IEnumerator LoadStreamingKey()
        {
            loadingStreamingKey=true;
            string path=Path.Combine(Application.streamingAssetsPath,"cycling-secrets.json");
            if(path.StartsWith("jar:",StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("http:",StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("https:",StringComparison.OrdinalIgnoreCase))
            {
                using(var request=UnityWebRequest.Get(path))
                {
                    request.timeout=10;
                    yield return request.SendWebRequest();
                    if(request.result==UnityWebRequest.Result.Success)
                    {
                        try{apiKey=ParseKey(request.downloadHandler.text);}
                        catch{apiKey=null;}
                    }
                }
            }
            else if(File.Exists(path))
            {
                try{apiKey=ParseKey(File.ReadAllText(path));}
                catch{apiKey=null;}
            }
            loadingStreamingKey=false;
            if(UsableKey(apiKey))Debug.Log("[RoutePlannerService] Route credentials loaded from StreamingAssets.");
        }
        static string ParseKey(string json)
        {
            // APK text responses retain the UTF-8 BOM that desktop file readers remove.
            return JsonUtility.FromJson<Secrets>(json?.TrimStart('\uFEFF',' ','\t','\r','\n'))?.openRouteServiceApiKey?.Trim();
        }
        static bool UsableKey(string key)
        {
            if(string.IsNullOrWhiteSpace(key))return false;
            string value=key.Trim();
            return value!="=" && !value.Equals("YOUR_OPENROUTESERVICE_KEY",StringComparison.OrdinalIgnoreCase);
        }
        public void SearchCalgary(string address)
        {
            if(string.IsNullOrWhiteSpace(address)){Fail("Enter a Calgary address first.");return;}
            if(string.IsNullOrWhiteSpace(apiKey)){Fail(loadingStreamingKey?"Loading route credentials. Try again in a moment.":"Missing OpenRouteService API key.");return;}
            StartCoroutine(Get("https://api.openrouteservice.org/geocode/search?size=5&boundary.country=CA"+
                "&boundary.rect.min_lon=-114.35&boundary.rect.max_lon=-113.80"+
                "&boundary.rect.min_lat=50.80&boundary.rect.max_lat=51.25"+
                "&text="+UnityWebRequest.EscapeURL(address+", Calgary, Alberta")+
                "&api_key="+UnityWebRequest.EscapeURL(apiKey),
                body=>ParsePlaces(body),"Address search failed."));
        }
        public void BicycleRoute(Place start, Place end)
        {
            if(start==null || end==null){Fail("Choose both a valid start and finish address.");return;}
            if(string.IsNullOrWhiteSpace(apiKey)){Fail(loadingStreamingKey?"Loading route credentials. Try again in a moment.":"Missing OpenRouteService API key.");return;}
            string url="https://api.openrouteservice.org/v2/directions/cycling-regular/geojson";
            string lon0=start.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lat0=start.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lon1=end.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lat1=end.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string body="{\"coordinates\":[["+lon0+","+lat0+"],["+lon1+","+lat1+"]],\"instructions\":false}";
            StartCoroutine(Post(url,body,bodyText=>ParseRoute(bodyText),"Cycling route request failed."));
        }
        IEnumerator Get(string url,Action<string> success,string failure)
        {
            using(var request=UnityWebRequest.Get(url))
            {
                request.timeout=30;
                request.SetRequestHeader("Authorization",apiKey);
                request.SetRequestHeader("Accept","application/json");
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)
                {
                    string detail=request.responseCode>0?" HTTP "+request.responseCode:" ("+request.error+")";
                    string response=request.downloadHandler == null ? null : request.downloadHandler.text;
                    if(!string.IsNullOrWhiteSpace(response))
                    {
                        response=response.Replace("\r"," ").Replace("\n"," ");
                        if(response.Length>180)response=response.Substring(0,180);
                        detail+=": "+response;
                    }
                    Fail(failure+detail);
                    yield break;
                }
                if(success==null){Fail(failure+" Internal response handler was missing.");yield break;}
                if(request.downloadHandler==null || string.IsNullOrWhiteSpace(request.downloadHandler.text)){Fail(failure+" Empty server response.");yield break;}
                try{success(request.downloadHandler.text);}
                catch(Exception ex){Debug.LogError("[RoutePlannerService] Response parsing failed: "+ex.Message);Fail(failure+" Invalid server response.");}
            }
        }
        IEnumerator Post(string url,string json,Action<string> success,string failure)
        {
            using(var request=new UnityWebRequest(url,"POST"))
            {
                request.timeout=30;
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler=new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization",apiKey);
                request.SetRequestHeader("Content-Type","application/json");
                request.SetRequestHeader("Accept","application/geo+json, application/json");
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Fail(failure+" HTTP "+request.responseCode);yield break;}
                if(success==null){Fail(failure+" Internal response handler was missing.");yield break;}
                if(request.downloadHandler==null || string.IsNullOrWhiteSpace(request.downloadHandler.text)){Fail(failure+" Empty server response.");yield break;}
                try{success(request.downloadHandler.text);}
                catch(Exception ex){Debug.LogError("[RoutePlannerService] Response parsing failed: "+ex.Message);Fail(failure+" Invalid server response.");}
            }
        }
        void ParsePlaces(string json)
        {
            var found=new List<Place>();
            var collection=JsonUtility.FromJson<PlaceCollection>(json);
            if(collection==null || collection.features==null){Fail("No Calgary addresses found.");return;}
            foreach(var feature in collection.features)
            {
                if(feature?.geometry?.coordinates==null || feature.geometry.coordinates.Length<2 ||
                    string.IsNullOrWhiteSpace(feature.properties?.label))continue;
                double longitude=feature.geometry.coordinates[0];
                double latitude=feature.geometry.coordinates[1];
                if(longitude < -114.35 || longitude > -113.80 || latitude < 50.80 || latitude > 51.25)continue;
                found.Add(new Place{label=feature.properties.label,longitude=longitude,latitude=latitude});
            }
            if(found.Count==0){Fail("No Calgary addresses found.");return;} PlacesFound?.Invoke(found);
        }
        void ParseRoute(string json)
        {
            var result=new RouteResult();
            var summary=Regex.Match(json,"\\\"summary\\\"[\\s\\S]*?\\\"distance\\\"\\s*:\\s*([0-9.]+)[\\s\\S]*?\\\"duration\\\"\\s*:\\s*([0-9.]+)");
            if(summary.Success){result.distance=float.Parse(summary.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);result.duration=float.Parse(summary.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture);}
            var coords=Regex.Match(json,"\\\"coordinates\\\"\\s*:\\s*\\[(.*?)\\]\\s*\\]");
            if(coords.Success)foreach(Match m in Regex.Matches(coords.Groups[1].Value,"\\[\\s*([-0-9.]+)\\s*,\\s*([-0-9.]+)"))result.points.Add(new Place{longitude=double.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture),latitude=double.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture)});
            if(result.points.Count<2){Fail("OpenRouteService returned no usable route geometry.");return;} RouteFound?.Invoke(result);
        }
        void Fail(string message){Debug.LogError("[RoutePlannerService] "+message);Failed?.Invoke(message);}
    }
}
