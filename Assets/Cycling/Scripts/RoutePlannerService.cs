using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        public sealed class Place { public string label; public double longitude, latitude; }
        public sealed class RouteResult { public readonly List<Place> points = new List<Place>(); public float distance; public float duration; }
        public event Action<string> Failed;
        public event Action<List<Place>> PlacesFound;
        public event Action<RouteResult> RouteFound;

        string apiKey;
        void Awake() { LoadKey(); }
        void LoadKey()
        {
            apiKey=Environment.GetEnvironmentVariable("CALGARY_ORS_API_KEY");
            if(!string.IsNullOrWhiteSpace(apiKey))return;
            string path=Path.Combine(Application.persistentDataPath,"cycling-secrets.json");
            if(File.Exists(path))apiKey=JsonUtility.FromJson<Secrets>(File.ReadAllText(path))?.openRouteServiceApiKey;
        }
        public void SearchCalgary(string address)
        {
            if(string.IsNullOrWhiteSpace(apiKey)){Fail("Missing OpenRouteService API key.");return;}
            StartCoroutine(Get("https://api.openrouteservice.org/geocode/search?size=5&boundary.country=CA&text="+UnityWebRequest.EscapeURL(address+", Calgary, Alberta"),
                body=>ParsePlaces(body),"Address search failed."));
        }
        public void BicycleRoute(Place start, Place end)
        {
            if(string.IsNullOrWhiteSpace(apiKey)){Fail("Missing OpenRouteService API key.");return;}
            string url="https://api.openrouteservice.org/v2/directions/cycling-regular/geojson?start="+
                start.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+start.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)+
                "&end="+end.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+end.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            StartCoroutine(Get(url,body=>ParseRoute(body),"Cycling route request failed."));
        }
        IEnumerator Get(string url,Action<string> success,string failure)
        {
            using(var request=UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization",apiKey);
                request.SetRequestHeader("Accept","application/json");
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Fail(failure+" HTTP "+request.responseCode);yield break;}
                success(request.downloadHandler.text);
            }
        }
        void ParsePlaces(string json)
        {
            var found=new List<Place>();
            foreach(Match m in Regex.Matches(json,"\\\"label\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"[\\s\\S]*?\\\"coordinates\\\"\\s*:\\s*\\[\\s*([-0-9.]+)\\s*,\\s*([-0-9.]+)"))
                found.Add(new Place{label=m.Groups[1].Value,longitude=double.Parse(m.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture),latitude=double.Parse(m.Groups[3].Value,System.Globalization.CultureInfo.InvariantCulture)});
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
