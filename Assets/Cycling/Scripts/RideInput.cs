using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using InputXRController = UnityEngine.InputSystem.XR.XRController;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace CalgaryCycling
{
    // Reads keyboard/OpenXR actions and directly polls Meta-compatible XR controller features.
    public sealed class RideInput : IDisposable
    {
        readonly InputAction keys = new InputAction("Keys", InputActionType.Value);
        readonly InputAction stick;
        readonly InputAction pause, restart, menu, recenter, select, debug;
        readonly List<XRInputDevice> deviceBuffer = new List<XRInputDevice>();
        XRInputDevice leftController, rightController;
        readonly bool useRightStick;
        bool handCalibrated;
        float neutralYaw;
        public bool HandlebarActive { get; private set; }
        public float CurrentSteering { get; private set; }
        bool xrPause, xrRestart, xrMenu, xrSelect;
        bool previousLeftPrimary, previousLeftSecondary, previousRightSecondary, previousRightSelect;
        bool reportedLeft,reportedRight;
        public bool RightControllerDetected=>rightController.isValid || InputXRController.rightHand!=null;
        public bool LeftControllerDetected=>leftController.isValid || InputXRController.leftHand!=null;

        public RideInput(bool right)
        {
            useRightStick=right;
            keys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            keys.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            stick=new InputAction("Steering",InputActionType.Value,right?"<XRController>{RightHand}/thumbstick":"<XRController>{LeftHand}/thumbstick");
            pause=Button("Pause","<Keyboard>/space","<XRController>{LeftHand}/primaryButton");
            restart=Button("Restart","<Keyboard>/r","<XRController>{LeftHand}/secondaryButton");
            menu=Button("Menu","<Keyboard>/escape","<XRController>{RightHand}/secondaryButton");
            recenter=Button("Recenter","<Keyboard>/c");
            select=Button("Select","<XRController>{RightHand}/triggerPressed","<XRController>{RightHand}/trigger","<XRController>{RightHand}/primaryButton");
            debug=Button("Third person","<Keyboard>/v");
            keys.Enable();stick.Enable();
        }

        static InputAction Button(string name,params string[] bindings)
        {
            var action=new InputAction(name,InputActionType.Button);foreach(string binding in bindings)action.AddBinding(binding);action.Enable();return action;
        }

        public void Tick()
        {
            RefreshController(ref leftController,InputDeviceCharacteristics.Left);
            RefreshController(ref rightController,InputDeviceCharacteristics.Right);
            var left=InputXRController.leftHand;
            var right=InputXRController.rightHand;
            bool leftPrimary=Read(leftController,XRCommonUsages.primaryButton)||Pressed(left,"primaryButton");
            bool leftSecondary=Read(leftController,XRCommonUsages.secondaryButton)||Pressed(left,"secondaryButton");
            bool rightSecondary=Read(rightController,XRCommonUsages.secondaryButton)||Pressed(right,"secondaryButton");
            bool rightSelect=Read(rightController,XRCommonUsages.primaryButton)||Read(rightController,XRCommonUsages.triggerButton)||ReadTrigger(rightController)||
                Pressed(right,"primaryButton")||Pressed(right,"triggerPressed")||Pressed(right,"triggerButton")||AxisPressed(right,"trigger");
            xrPause=leftPrimary&&!previousLeftPrimary;xrRestart=leftSecondary&&!previousLeftSecondary;xrMenu=rightSecondary&&!previousRightSecondary;xrSelect=rightSelect&&!previousRightSelect;
            previousLeftPrimary=leftPrimary;previousLeftSecondary=leftSecondary;previousRightSecondary=rightSecondary;previousRightSelect=rightSelect;
            if(reportedLeft!=LeftControllerDetected || reportedRight!=RightControllerDetected)
            {
                reportedLeft=LeftControllerDetected;reportedRight=RightControllerDetected;
                Debug.Log("[RideInput] Controllers detected: left="+reportedLeft+", right="+reportedRight);
            }
            if(xrSelect)Debug.Log("[RideInput] Right-controller select button detected.");
        }

        void RefreshController(ref XRInputDevice device,InputDeviceCharacteristics hand)
        {
            if(device.isValid)return;
            deviceBuffer.Clear();InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller|hand,deviceBuffer);
            if(deviceBuffer.Count>0)device=deviceBuffer[0];
        }

        static bool Pressed(InputXRController controller,string control)
        {
            return controller?.TryGetChildControl<ButtonControl>(control)?.isPressed==true;
        }
        static bool AxisPressed(InputXRController controller,string control)
        {
            var axis=controller?.TryGetChildControl<AxisControl>(control);
            return axis!=null && axis.ReadValue()>.5f;
        }

        static bool Read(XRInputDevice device,InputFeatureUsage<bool> usage)
        {
            bool value;return device.isValid&&device.TryGetFeatureValue(usage,out value)&&value;
        }
        static bool ReadTrigger(XRInputDevice device)
        {
            float value;return device.isValid&&device.TryGetFeatureValue(XRCommonUsages.trigger,out value)&&value>.5f;
        }

        public float Steering(float deadZone)
        {
            HandlebarActive=false;
            float keyboard=keys.ReadValue<float>();
            if(Mathf.Abs(keyboard)>.01f){CurrentSteering=keyboard;return keyboard;}
            bool leftReady=TryHand(leftController,InputXRController.leftHand,out Vector3 left);
            bool rightReady=TryHand(rightController,InputXRController.rightHand,out Vector3 right);
            Vector3 span=right-left;span.y=0; // Height, wrist pitch and roll never steer.
            if(leftReady && rightReady && span.sqrMagnitude>=.04f && span.sqrMagnitude<2.25f)
            {
                float yaw=Mathf.Atan2(-span.z,span.x)*Mathf.Rad2Deg;
                if(!handCalibrated){neutralYaw=yaw;handCalibrated=true;}
                float angle=Mathf.DeltaAngle(neutralYaw,yaw);
                HandlebarActive=true;
                CurrentSteering=ApplyDeadZone(Mathf.Clamp(angle/35f,-1,1),.05f);
                return CurrentSteering;
            }
            handCalibrated=false;
            Vector2 axis=stick.ReadValue<Vector2>();
            var selected=useRightStick?rightController:leftController;
            if(axis.sqrMagnitude<.001f && selected.isValid)selected.TryGetFeatureValue(XRCommonUsages.primary2DAxis,out axis);
            var controller=useRightStick?InputXRController.rightHand:InputXRController.leftHand;
            if(axis.sqrMagnitude<.001f && controller!=null)
            {
                var thumbstick=controller.TryGetChildControl<Vector2Control>("thumbstick") ??
                    controller.TryGetChildControl<Vector2Control>("primary2DAxis");
                if(thumbstick!=null)axis=thumbstick.ReadValue();
            }
            CurrentSteering=ApplyDeadZone(axis.x,deadZone);
            return CurrentSteering;
        }

        static bool TryHand(XRInputDevice device,InputXRController controller,out Vector3 point)
        {
            if(device.isValid)
            {
                bool isTracked;
                float squeeze;
                bool holding;
                bool valid=device.TryGetFeatureValue(XRCommonUsages.isTracked,out isTracked) && isTracked;
                bool gripping=(device.TryGetFeatureValue(XRCommonUsages.grip,out squeeze) && squeeze>.55f) ||
                    (device.TryGetFeatureValue(XRCommonUsages.gripButton,out holding) && holding);
                bool hasPosition=device.TryGetFeatureValue(XRCommonUsages.devicePosition,out point);
                if(valid && gripping && hasPosition)return true;
            }
            point=controller==null?Vector3.zero:controller.devicePosition.ReadValue();
            if(controller==null || !controller.isTracked.isPressed)return false;
            var grip=controller.TryGetChildControl<AxisControl>("grip");
            var gripButton=controller.TryGetChildControl<ButtonControl>("gripPressed") ??
                controller.TryGetChildControl<ButtonControl>("gripButton");
            return (grip!=null && grip.ReadValue()>.55f) || (gripButton!=null && gripButton.isPressed);
        }

        static float ApplyDeadZone(float value,float deadZone)
        {
            return Mathf.Abs(value)<=deadZone?0:Mathf.Sign(value)*Mathf.InverseLerp(deadZone,1,Mathf.Abs(value));
        }

        public void CalibrateSteering() { handCalibrated=false;CurrentSteering=0;HandlebarActive=false; }

        public bool Pause=>pause.WasPressedThisFrame()||xrPause;
        public bool Restart=>restart.WasPressedThisFrame()||xrRestart;
        public bool Menu=>menu.WasPressedThisFrame()||xrMenu;
        public bool Recenter=>recenter.WasPressedThisFrame();
        public bool Select=>select.WasPressedThisFrame()||xrSelect;
        public bool DebugView=>debug.WasPressedThisFrame();
        public void Dispose(){keys.Dispose();stick.Dispose();pause.Dispose();restart.Dispose();menu.Dispose();recenter.Dispose();select.Dispose();debug.Dispose();}
    }
}
