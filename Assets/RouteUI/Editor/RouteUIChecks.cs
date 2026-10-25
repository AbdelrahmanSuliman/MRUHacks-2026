using System;
using CalgaryCycling.RouteUI;
using UnityEditor;
using UnityEngine;

namespace CalgaryCycling.Editor
{
    public static class RouteUIChecks
    {
        [MenuItem("Calgary Cycling/Run Route UI Data Checks")]
        public static void Run()
        {
            var asset=Resources.Load<TextAsset>("RouteUI/OfflineRoute");
            Require(asset!=null,"Bundled offline route loads through Resources.");
            Require(OfflineRouteSummary.TryParse(asset.text,out var route),"Export summary parses.");
            Require(Math.Abs(route.distanceMeters-1421f)<.01f,"Fixture distance is 1421 metres.");
            Require(Math.Abs(route.durationSeconds-320.4f)<.01f,"Fixture duration is 320.4 seconds.");
            Require(route.origin.longitude<0 && route.origin.latitude>0,"Calgary coordinate order.");
            Require(route.PreviewText.Contains("1.42 km") && route.PreviewText.Contains("5.3 min"),"Display units.");
            Require(!OfflineRouteSummary.TryParse("{",out _),"Malformed JSON rejected.");
            Require(!OfflineRouteSummary.TryParse("{}",out _),"Empty object rejected.");
            Require(!OfflineRouteSummary.TryParse(asset.text.Replace("1421.0","-1"),out _),"Negative distance rejected.");
            Require(!OfflineRouteSummary.TryParse(asset.text.Replace("\"distanceMeters\": 1421.0,",""),out _),"Missing distance rejected.");
            Require(!OfflineRouteSummary.TryParse(asset.text.Replace("-114.0631","-214.0631"),out _),"Invalid endpoint rejected.");
            Debug.Log("PASS: offline route loading, JSON validation, coordinates and display units. UI appearance and input still need Play mode testing.");
        }

        static void Require(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("FAIL: "+message);
        }
    }
}
