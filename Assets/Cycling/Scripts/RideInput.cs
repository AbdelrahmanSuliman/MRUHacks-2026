using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CalgaryCycling
{
    // Action-based OpenXR input. The same normalized steering feeds the same motor on both platforms.
    public sealed class RideInput : IDisposable
    {
        readonly InputAction keys = new InputAction("Keys", InputActionType.Value);
        readonly InputAction stick;
        readonly InputAction handRotation;
        readonly InputAction pause, restart, menu, recenter, select, debug;
        bool handCalibrated; float neutralYaw;
        public RideInput(bool right)
        {
            keys.AddCompositeBinding("1DAxis").With("Negative","<Keyboard>/a").With("Positive","<Keyboard>/d");
            keys.AddCompositeBinding("1DAxis").With("Negative","<Keyboard>/leftArrow").With("Positive","<Keyboard>/rightArrow");
            stick=new InputAction("Steering",InputActionType.Value,
                right?"<XRController>{RightHand}/thumbstick":"<XRController>{LeftHand}/thumbstick");
            handRotation=new InputAction("Hand steering",InputActionType.Value,"<XRController>{LeftHand}/deviceRotation");
            pause=Button("Pause","<Keyboard>/space","<XRController>{LeftHand}/primaryButton");
            restart=Button("Restart","<Keyboard>/r","<XRController>{LeftHand}/secondaryButton");
            menu=Button("Menu","<Keyboard>/escape","<XRController>{RightHand}/secondaryButton");
            recenter=Button("Recenter","<Keyboard>/c","<XRController>{RightHand}/primaryButton");
            select=Button("Select","<XRController>{RightHand}/triggerPressed");
            debug=Button("Third person","<Keyboard>/v");
            keys.Enable();stick.Enable();handRotation.Enable();
        }
        static InputAction Button(string name, params string[] bindings)
        {
            var a=new InputAction(name,InputActionType.Button);
            foreach(string b in bindings)a.AddBinding(b); a.Enable();return a;
        }
        public float Steering(float deadZone)
        {
            float k=keys.ReadValue<float>();
            if(Mathf.Abs(k)>.01f)return k;
            float x=stick.ReadValue<Vector2>().x;
            if(Mathf.Abs(x)>deadZone)return Mathf.Sign(x)*Mathf.InverseLerp(deadZone,1,Mathf.Abs(x));
            var rotation=handRotation.ReadValue<Quaternion>();
            float yaw=Mathf.DeltaAngle(0,rotation.eulerAngles.y);
            if(!handCalibrated){neutralYaw=yaw;handCalibrated=true;}
            float hand=Mathf.DeltaAngle(neutralYaw,yaw);
            return Mathf.Clamp(hand/35f,-1,1);
        }
        public bool Pause => pause.WasPressedThisFrame();
        public bool Restart => restart.WasPressedThisFrame();
        public bool Menu => menu.WasPressedThisFrame();
        public bool Recenter => recenter.WasPressedThisFrame();
        public bool Select => select.WasPressedThisFrame();
        public bool DebugView => debug.WasPressedThisFrame();
        public void Dispose()
        {
            keys.Dispose();stick.Dispose();handRotation.Dispose();pause.Dispose();restart.Dispose();menu.Dispose();
            recenter.Dispose();select.Dispose();debug.Dispose();
        }
    }
}
