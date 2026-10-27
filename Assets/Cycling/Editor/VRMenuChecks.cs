using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CalgaryCycling.Editor
{
    public static class VRMenuChecks
    {
        [MenuItem("Calgary Cycling/Validation/Check VR Start Button %#F6")]
        public static void Run()
        {
            string result;
            try { QuestBuild.ValidateControllers();Check();result="PASS: Android Quest 2 profile enabled; A and trigger detected; held buttons do not repeat; Start Ride button action enters Riding; ready status is clear. Spatial gaze is not exercised in Edit Mode."; }
            catch(Exception ex) { result="FAIL: "+ex.GetBaseException(); }
            File.WriteAllText("Temp/CyclingVRMenuChecks.txt",result);
            Debug.Log("[VRMenuChecks] "+result);
        }

        static void Check()
        {
            InputSystem.RegisterLayout(@"{""name"":""CyclingMenuTestController"",""extend"":""XRController"",""controls"":[
                {""name"":""primaryButton"",""layout"":""Button""},
                {""name"":""trigger"",""layout"":""Axis""} ]}");
            var right=(XRController)InputSystem.AddDevice("CyclingMenuTestController");
            InputSystem.SetDeviceUsage(right,UnityEngine.InputSystem.CommonUsages.RightHand);
            var root=new GameObject("VR menu regression");
            var config=ScriptableObject.CreateInstance<RideSettings>();
            RideInput input=null;
            try
            {
                var app=root.AddComponent<RideApp>();
                Set(app,"Settings",config);
                var motorObject=new GameObject("Bicycle");motorObject.transform.SetParent(root.transform,false);
                var motor=motorObject.AddComponent<BicycleMotor>();
                motor.Initialize(new RoutePath(new[]{Vector3.zero,new Vector3(0,0,50)},false),config);
                Set(app,"Motor",motor);
                var rigObject=new GameObject("XR Origin");rigObject.transform.SetParent(motorObject.transform,false);
                var rig=rigObject.AddComponent<RiderCamera>();rig.Initialize(config.eyeHeight);Set(app,"Rig",rig);
                var scenery=root.AddComponent<SceneryService>();Set(scenery,"Ready",true);Set(app,"Scenery",scenery);
                input=new RideInput(false);Set(app,"Input",input);
                // A player build can invalidate the Editor's cached default UI asset between replays.
                typeof(InputSystemUIInputModule).GetField("defaultActions",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,null);
                var ui=root.AddComponent<VeloUIManager>();ui.Initialize(app);
                var start=Array.Find(root.GetComponentsInChildren<Button>(true),b=>b.name=="START RIDE");
                SetButton(right.isTracked,true);input.Tick();
                SetButton(right.GetChildControl<ButtonControl>("primaryButton"),true);input.Tick();
                if(!input.Select)throw new Exception("A button on the tracked right controller was not detected.");
                input.Tick();
                // Direct-poll edges must not repeat while a button is held.
                var edge=(bool)typeof(RideInput).GetField("xrSelect",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(input);
                if(edge)throw new Exception("Holding A repeats the select event.");
                SetButton(right.GetChildControl<ButtonControl>("primaryButton"),false);input.Tick();
                InputSystem.QueueDeltaStateEvent(right.GetChildControl<AxisControl>("trigger"),1f);
                InputSystem.Update();input.Tick();
                if(!input.Select)throw new Exception("Right trigger was not detected.");
                // Edit Mode does not render the gaze raycast. Exercise the actual button callback
                // separately; device logs verify button delivery through the live spatial menu.
                start.onClick.Invoke();
                if(app.State!=RideState.Riding)throw new Exception("Start Ride button action cannot start the ready ride.");
                app.Menu();
                typeof(VeloUIManager).GetMethod("UpdateDetails",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);
                var status=(Text)typeof(VeloUIManager).GetField("status",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
                if(!status.text.StartsWith("Ready to ride"))throw new Exception("Ready scenery does not clearly say it is ready.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                input?.Dispose();
                if(config!=null)UnityEngine.Object.DestroyImmediate(config);
                InputSystem.RemoveDevice(right);InputSystem.RemoveLayout("CyclingMenuTestController");
            }
        }

        static void Set(object owner,string property,object value)
        {
            owner.GetType().GetField("<"+property+">k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
        }

        static void SetButton(ButtonControl button,bool pressed)
        {
            // XR button states are bit fields; send a full state event rather than a byte delta.
            using(StateEvent.From(button.device,out var eventPtr))
            {
                button.WriteValueIntoEvent(pressed?1f:0f,eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }
    }
}
