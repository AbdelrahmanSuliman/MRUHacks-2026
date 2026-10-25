using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;

namespace CalgaryCycling.Editor
{
    // Exercises the actual imported-art builder and input reader, without a headset or network.
    public static class RideProductChecks
    {
        const string ReportPath = "Temp/CyclingProductChecks.txt";

        [MenuItem("Calgary Cycling/Validation/Inspect Active Ride %#F10")]
        public static void InspectActiveRide()
        {
            var lines=new List<string>();
            var app=UnityEngine.Object.FindFirstObjectByType<RideApp>();
            if(app==null || app.Motor==null){Debug.LogWarning("Start Play Mode before inspecting the active ride.");return;}
            lines.Add("State: "+app.State+"; route: "+app.Motor.Route.Length.ToString("F1")+" m");
            var environment=app.Motor.transform.parent.Find("Imported Stylized Calgary Environment");
            if(environment!=null)
            {
                var clearance=new RouteClearance(app.Motor.Route,app.Settings.corridorHalfWidth+.7f);
                int count=0,overlaps=0;
                foreach(Transform child in environment)
                    if(child.gameObject.activeSelf && child.name.StartsWith("Imported"))
                    {
                        count++;
                        if(RouteClearance.TryFootprint(child,environment,out var bounds) && clearance.Overlaps(bounds))overlaps++;
                    }
                lines.Add("Imported objects: "+count+"; corridor overlaps: "+overlaps);
            }
            foreach(var ui in Resources.FindObjectsOfTypeAll<UnityEngine.UIElements.UIDocument>())
                lines.Add("UIDocument: "+ui.name+" active="+ui.gameObject.activeInHierarchy+" texture="+(ui.panelSettings==null?"none":ui.panelSettings.targetTexture==null?"SCREEN":ui.panelSettings.targetTexture.name));
            lines.Add("Camera: field of view="+app.Rig.View.fieldOfView+" aspect="+app.Rig.View.aspect);
            foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                lines.Add("Canvas: "+c.name+" position="+c.transform.position+" scale="+c.transform.lossyScale);
            File.WriteAllLines("Temp/CyclingActiveRide.txt",lines);
            Debug.Log("[RideProductChecks] "+string.Join("\n",lines));
        }

        [MenuItem("Calgary Cycling/Validation/Check Ride Product %#F9")]
        public static void Run()
        {
            var lines = new List<string>();
            var errors=new List<string>();
            Application.LogCallback capture=(message,stack,type)=>{if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=capture;
            try
            {
            Check(lines, "Imported scenery stays outside the entire ride corridor", CheckScenery);
            Check(lines, "Only horizontal two-hand handlebar movement steers", CheckSteering);
            Check(lines, "Route geometry, steering limits and arrival", CoreChecks.Run);
            Check(lines,"Address names stay paired with their own coordinates",CheckAddressPairing);
            }
            finally { Application.logMessageReceived-=capture; }
            lines.Add(errors.Count==0?"PASS: No unexpected Unity errors during validation":"FAIL: Unity errors: "+string.Join("; ",errors));
            Directory.CreateDirectory("Temp");
            File.WriteAllLines(ReportPath, lines);
            foreach (string line in lines)
                if (line.StartsWith("PASS")) Debug.Log("[RideProductChecks] " + line);
                else Debug.LogError("[RideProductChecks] " + line);
        }

        static void Check(List<string> lines, string name, Action test)
        {
            try { test(); lines.Add("PASS: " + name); }
            catch (Exception ex) { lines.Add("FAIL: " + name + " - " + ex.Message); }
        }

        static void CheckScenery()
        {
            var config = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<RideSettings>("Assets/Cycling/Settings/Calgary.asset"));
            var container = new GameObject("Scenery clearance check");
            var violations = new List<string>();
            int imported = 0;
            try
            {
                // A tight return bend catches props placed safely beside one segment
                // but across another segment of the same route.
                var route = new RoutePath(new[] { Vector3.zero, new Vector3(0,0,90), new Vector3(12,0,90),
                    new Vector3(12,0,0), new Vector3(30,0,0), new Vector3(30,0,120) }, false);
                foreach (StylizedZone zone in Enum.GetValues(typeof(StylizedZone)))
                {
                    var environment = new StylizedEnvironmentBuilder(config, container.transform);
                    Transform built = environment.Build(route, zone, true);
                    foreach (Transform child in built)
                    {
                        if (!child.gameObject.activeSelf || !child.name.StartsWith("Imported")) continue;
                        imported++;
                        var renderers = child.GetComponentsInChildren<Renderer>();
                        if (renderers.Length == 0) continue;
                        Bounds bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        bounds.Expand(new Vector3(2*(config.corridorHalfWidth+.7f), 0, 2*(config.corridorHalfWidth+.7f)));
                        // An independent dense sampling oracle, rather than the placement algorithm.
                        for (float d=0; d<=route.Length; d+=.2f)
                        {
                            var p=route.Position(d);
                            if(p.x<bounds.min.x || p.x>bounds.max.x || p.z<bounds.min.z || p.z>bounds.max.z)continue;
                            violations.Add(zone+" "+child.name+" at "+d.ToString("F1")+" m");
                            break;
                        }
                    }
                    environment.Dispose();
                }
                if(imported==0)throw new Exception("No actual imported prefabs instantiated.");
                if(violations.Count>0)throw new Exception(violations.Count+" overlaps; "+string.Join("; ",violations.GetRange(0,Mathf.Min(3,violations.Count))));
            }
            finally { UnityEngine.Object.DestroyImmediate(container); UnityEngine.Object.DestroyImmediate(config); }
        }

        static void CheckAddressPairing()
        {
            var go=new GameObject("Address parser check");
            try
            {
                var service=go.AddComponent<RoutePlannerService>();
                List<RoutePlannerService.Place> places=null;
                service.PlacesFound+=found=>places=found;
                // GeoJSON puts geometry before properties; a cross-feature regex shifts addresses.
                const string json=@"{""features"":[
                    {""geometry"":{""coordinates"":[-114.07883,51.05389]},""properties"":{""label"":""Peace Bridge, Calgary""}},
                    {""properties"":{""label"":""River path, Calgary""},""geometry"":{""coordinates"":[-114.03,51.05]}}
                ]}";
                typeof(RoutePlannerService).GetMethod("ParsePlaces",System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(service,new object[]{json});
                Require(places!=null && places.Count==2,"Geocoder lost a result or paired labels across feature boundaries.");
                Require(places[0].label.StartsWith("Peace Bridge") && Math.Abs(places[0].longitude+114.07883)<.000001,"The selected start has another place's coordinates.");
                Require(Math.Abs(places[1].longitude+114.03)<.000001,"The finish coordinates changed.");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }

        static void CheckSteering()
        {
            InputSystem.RegisterLayout(@"{""name"":""CyclingTestController"",""extend"":""XRController"",""controls"": [
                {""name"":""grip"",""layout"":""Axis""}, {""name"":""thumbstick"",""layout"":""Vector2""} ]}");
            var left=(XRController)InputSystem.AddDevice("CyclingTestController");
            var right=(XRController)InputSystem.AddDevice("CyclingTestController");
            InputSystem.SetDeviceUsage(left, UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, UnityEngine.InputSystem.CommonUsages.RightHand);
            try
            {
                using(var input=new RideInput(false))
                {
                    Pose(left,new Vector3(-.3f,1,.4f),Quaternion.identity,false);
                    Pose(right,new Vector3(.3f,1,.4f),Quaternion.identity,false);
                    input.Tick(); input.Steering(.15f);
                    Pose(left,new Vector3(-.3f,1,.4f),Quaternion.Euler(0,30,0),false);
                    InputSystem.Update(); input.Tick();
                    Require(Mathf.Abs(input.Steering(.15f))<.01f,"Unheld controller rotation steers the bike.");
                    Pose(left,new Vector3(-.3f,1,.4f),Quaternion.identity,true);
                    Pose(right,new Vector3(.3f,1,.4f),Quaternion.identity,true);
                    input.Tick(); Require(Mathf.Abs(input.Steering(.15f))<.01f,"Grip calibration jumps.");
                    Pose(left,new Vector3(-.3f,1.5f,.4f),Quaternion.Euler(50,0,30),true);
                    input.Tick(); Require(Mathf.Abs(input.Steering(.15f))<.01f,"Lifting or rolling a hand steers.");
                    Pose(left,new Vector3(-.26f,1,.55f),Quaternion.identity,true);
                    Pose(right,new Vector3(.26f,1,.25f),Quaternion.identity,true);
                    input.Tick(); float rightTurn=input.Steering(.15f);
                    Require(rightTurn>.35f,"Turning handlebars right does not steer right: output="+rightTurn+
                        ", active="+input.HandlebarActive+", L="+left.devicePosition.ReadValue()+", R="+right.devicePosition.ReadValue()+
                        ", tracked="+left.isTracked.ReadValue()+"/"+right.isTracked.ReadValue()+", grips="+
                        left.GetChildControl<AxisControl>("grip").ReadValue()+"/"+right.GetChildControl<AxisControl>("grip").ReadValue());
                    Pose(left,new Vector3(-.26f,1,.25f),Quaternion.identity,true);
                    Pose(right,new Vector3(.26f,1,.55f),Quaternion.identity,true);
                    input.Tick(); Require(input.Steering(.15f)<-.35f,"Turning handlebars left does not steer left.");
                    Pose(left,new Vector3(-.3f,1,.4f),Quaternion.identity,false);
                    Pose(right,new Vector3(.3f,1,.4f),Quaternion.identity,false);
                    input.Tick(); Require(Mathf.Abs(input.Steering(.15f))<.01f,"Releasing grips leaves steering active.");
                    InputSystem.QueueDeltaStateEvent(left.GetChildControl<Vector2Control>("thumbstick"),new Vector2(0,1));
                    InputSystem.Update();input.Tick();Require(Mathf.Abs(input.Steering(.15f))<.01f,"Thumbstick up/down steers the bike.");
                    InputSystem.QueueDeltaStateEvent(left.GetChildControl<Vector2Control>("thumbstick"),new Vector2(.8f,0));
                    InputSystem.Update();input.Tick();Require(input.Steering(.15f)>.5f,"Left/right thumbstick fallback is unavailable.");
                    InputSystem.QueueDeltaStateEvent(left.GetChildControl<Vector2Control>("thumbstick"),Vector2.zero);
                    Pose(left,new Vector3(-.3f,1,.4f),Quaternion.identity,true);
                    Pose(right,new Vector3(.3f,1,.4f),Quaternion.identity,true);
                    input.Tick();Require(Mathf.Abs(input.Steering(.15f))<.01f,"Regripping fails to centre steering.");
                    using(StateEvent.From(left,out var trackingEvent))
                    {
                        left.isTracked.WriteValueIntoEvent(0f,trackingEvent);
                        InputSystem.QueueEvent(trackingEvent);
                    }
                    InputSystem.Update();input.Tick();Require(Mathf.Abs(input.Steering(.15f))<.01f,"Tracking loss leaves steering active.");
                }
            }
            finally { InputSystem.RemoveDevice(left); InputSystem.RemoveDevice(right); InputSystem.RemoveLayout("CyclingTestController"); }
        }

        static void Pose(XRController device,Vector3 position,Quaternion rotation,bool gripping)
        {
            using(StateEvent.From(device,out var eventPtr))
            {
                device.devicePosition.WriteValueIntoEvent(position,eventPtr);
                device.deviceRotation.WriteValueIntoEvent(rotation,eventPtr);
                device.isTracked.WriteValueIntoEvent(1f,eventPtr);
                device.GetChildControl<AxisControl>("grip").WriteValueIntoEvent(gripping?1f:0f,eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }
        static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    }
}
