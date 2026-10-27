using System;
using UnityEditor;
using UnityEngine;
namespace CalgaryCycling.Editor
{
    public static class CoreChecks
    {
        static void Require(bool condition,string message) {if(!condition)throw new Exception("FAIL: "+message);}
        [MenuItem("Calgary Cycling/Run Core Checks")]
        public static void Run()
        {
            bool threw=false;
            try {new RoutePath(new[]{Vector3.zero,Vector3.zero});}catch(ArgumentException){threw=true;}
            Require(threw,"Duplicate points must fail safely.");
            var route=new RoutePath(new[]{Vector3.zero,new Vector3(0,0,50),new Vector3(0,0,100)});
            Require(Mathf.Abs(route.Length-100)<.01f,"Straight route length.");
            Require(Vector3.Distance(route.Position(-100),Vector3.zero)<.001f,"Start clamp.");
            Require(Vector3.Distance(route.Position(999),new Vector3(0,0,100))<.001f,"End clamp.");
            var streetRoute=new RoutePath(new[]{Vector3.zero,new Vector3(.2f,0,0),
                new Vector3(0,0,5),new Vector3(5,0,5)},false);
            Require(Mathf.Abs(streetRoute.Length-10)<.01f,"Street route filters near-duplicate points.");
            Require(Vector3.Distance(streetRoute.Position(4.9f),new Vector3(0,0,4.9f))<.01f,
                "Street route preserves the approach to a turn.");
            Require(Vector3.Distance(streetRoute.Position(5.1f),new Vector3(.1f,0,5))<.01f,
                "Street route preserves the exit from a turn.");
            var cfg=ScriptableObject.CreateInstance<RideSettings>();
            var go=new GameObject("Motor test temporary");
            try
            {
                var motor=go.AddComponent<BicycleMotor>();motor.Initialize(route,cfg);
                for(int i=0;i<300;i++)motor.Tick(1,1f/60);
                Require(motor.Distance>0,"Automatic forward motion.");
                Require(motor.LateralOffset>0 && go.transform.localPosition.x>0,"Steering changes world position.");
                for(int i=0;i<600;i++)motor.Tick(1,1f/60);
                Require(motor.LateralOffset<=cfg.corridorHalfWidth+.0001f,"Right corridor bound.");
                for(int i=0;i<600;i++)motor.Tick(-1,1f/60);
                Require(motor.LateralOffset<0,"Direction reversal.");
                Require(motor.LateralOffset>=-cfg.corridorHalfWidth-.0001f,"Left corridor bound.");
                motor.ResetRide();Require(motor.Distance==0 && motor.LateralOffset==0 && motor.Speed==0,"Restart clears state.");
                motor.Tick(0,1f/60);Require(motor.Speed<=cfg.acceleration/60+.0001f,"Acceleration bound.");
                for(int i=0;i<12000 && !motor.Arrived;i++)motor.Tick(0,1f/60);
                Require(motor.Arrived && motor.Speed==0,"Destination stops motion.");
                float d=motor.Distance;motor.Tick(1,1);Require(motor.Distance==d,"No movement after arrival.");
                Debug.Log("PASS: route validation, street geometry, sampling, motion, actual steering, bounds, reset, acceleration, arrival.");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cfg);}
        }
    }
}
