using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
namespace CalgaryCycling
{
    public sealed class RiderCamera : MonoBehaviour
    {
        public Camera View { get; private set; }
        public bool IsVR { get; private set; }
        readonly List<XRDisplaySubsystem> displays=new List<XRDisplaySubsystem>();
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        XROrigin origin;
        TrackedPoseDriver driver;
        Transform offset;
        float height;
        bool thirdPerson;
        public void Initialize(float eyeHeight)
        {
            height=eyeHeight;
            origin=gameObject.AddComponent<XROrigin>();
            offset=new GameObject("Camera Offset").transform;offset.SetParent(transform,false);
            var cameraGO=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
            cameraGO.tag="MainCamera";cameraGO.transform.SetParent(offset,false);
            View=cameraGO.GetComponent<Camera>();View.nearClipPlane=.08f;View.farClipPlane=1500;
            View.clearFlags=CameraClearFlags.SolidColor;View.backgroundColor=new Color(.13f,.2f,.27f);
            View.allowHDR=false;View.allowMSAA=true;
            origin.Camera=View;origin.CameraFloorOffsetObject=offset.gameObject;
            origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset=height;
            driver=cameraGO.AddComponent<TrackedPoseDriver>();driver.enabled=false;
            driver.positionInput=new InputActionProperty(new InputAction("Head Position",InputActionType.Value,"<XRHMD>/centerEyePosition"));
            driver.rotationInput=new InputActionProperty(new InputAction("Head Rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation"));
            driver.trackingStateInput=new InputActionProperty(new InputAction("Head Tracking",InputActionType.Value,"<XRHMD>/trackingState"));
            driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            DesktopPose();
        }
        void Update()
        {
            if(View==null)return;
            SubsystemManager.GetSubsystems(displays);
            bool running=false;foreach(var d in displays)running|=d.running;
            if(running==IsVR)return;
            IsVR=running;driver.enabled=running;
            if(running)
            {
                thirdPerson=false;
                View.transform.localPosition=Vector3.zero;View.transform.localRotation=Quaternion.identity;
                origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;
                origin.CameraYOffset=height;
            }
            else DesktopPose();
        }
        void DesktopPose()
        {
            offset.localPosition=Vector3.up*height;
            View.transform.localPosition=thirdPerson?new Vector3(0,1,-4):Vector3.zero;
            View.transform.localRotation=thirdPerson?Quaternion.Euler(10,0,0):Quaternion.identity;
        }
        public void ToggleDebug() { if(IsVR)return;thirdPerson=!thirdPerson;DesktopPose(); }
        public bool Recenter()
        {
            if(!IsVR) { thirdPerson=false;DesktopPose();return true; }
            SubsystemManager.GetSubsystems(inputs);bool success=false;
            foreach(var i in inputs)if(i.running)success|=i.TryRecenter();
            return success;
        }
        void OnDestroy()
        {
            if(driver==null)return;
            driver.enabled=false;
            driver.positionInput.action?.Dispose();driver.rotationInput.action?.Dispose();
            driver.trackingStateInput.action?.Dispose();
        }
    }
}
