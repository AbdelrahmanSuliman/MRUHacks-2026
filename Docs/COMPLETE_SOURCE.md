# Complete implementation source

> Historical appendix: this listing is not guaranteed to match the current implementation. Use the files under `Assets/Cycling` as the executable source of truth. In particular, scenery credentials and tileset configuration have changed since this snapshot.

Each path is relative to the Unity project root. See FILE_GUIDE.md for Inspector wiring and individual tests.

## Packages/manifest.json

```json
{
  "dependencies": {
    "com.cesium.unity": "1.18.1",
    "com.meta.xr.sdk.core": "77.0.0",
    "com.unity.inputsystem": "1.11.2",
    "com.unity.xr.openxr": "1.14.0",
    "com.unity.xr.management": "4.4.0",
    "com.unity.xr.core-utils": "2.3.0",
    "com.unity.render-pipelines.universal": "17.0.4",
    "com.unity.mathematics": "1.3.2",
    "com.unity.ugui": "2.0.0",
    "com.unity.test-framework": "1.4.5",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0",
    "com.unity.modules.xr": "1.0.0",
    "com.unity.shadergraph": "17.0.4"
  },
  "scopedRegistries": [
    {
      "name": "Cesium",
      "url": "https://unity.pkg.cesium.com",
      "scopes": [
        "com.cesium.unity"
      ]
    }
  ]
}
```

## ProjectSettings/ProjectVersion.txt

```text
m_EditorVersion: 6000.0.66f2
```

## .gitignore

```text
Library/
Temp/
Obj/
Logs/
UserSettings/
Builds/
.vs/
*.csproj
*.sln
**/Secrets/
**/cycling-secrets.json
```

## Assets/Cycling/Editor/CoreChecks.cs

```csharp
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
                Debug.Log("PASS: route validation, sampling, motion, actual steering, bounds, reset, acceleration, arrival.");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cfg);}
        }
    }
}
```

## Assets/Cycling/Editor/CreateDemoScene.cs

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace CalgaryCycling.Editor
{
    public static class CreateDemoScene
    {
        [MenuItem("Calgary Cycling/Create Demo Scene")]
        public static void Create()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory("Assets/Cycling/Settings");Directory.CreateDirectory("Assets/Cycling/Scenes");
            var settings=AssetDatabase.LoadAssetAtPath<RideSettings>("Assets/Cycling/Settings/Calgary.asset");
            if(settings==null) {settings=ScriptableObject.CreateInstance<RideSettings>();AssetDatabase.CreateAsset(settings,"Assets/Cycling/Settings/Calgary.asset");}
            string rendererPath="Assets/Cycling/Settings/CyclingRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if(renderer==null) {renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,rendererPath);}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Cycling/Settings/CyclingURP.asset");
            if(pipeline==null)
            {
                pipeline=UniversalRenderPipelineAsset.Create(renderer);
                pipeline.msaaSampleCount=2;pipeline.renderScale=.85f;pipeline.supportsHDR=false;
                AssetDatabase.CreateAsset(pipeline,"Assets/Cycling/Settings/CyclingURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline=pipeline;
            // Runtime-created materials still require a retained shader in player builds.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var included=graphics.FindProperty("m_AlwaysIncludedShaders");
            var unlit=Shader.Find("Universal Render Pipeline/Unlit");
            bool found=false;
            for(int j=0;j<included.arraySize;j++)found|=included.GetArrayElementAtIndex(j).objectReferenceValue==unlit;
            if(!found && unlit!=null) {included.InsertArrayElementAtIndex(included.arraySize);included.GetArrayElementAtIndex(included.arraySize-1).objectReferenceValue=unlit;graphics.ApplyModifiedProperties();}
            for(int i=0;i<QualitySettings.names.Length;i++)
            {QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
            QualitySettings.shadows=ShadowQuality.Disable;
            PlayerSettings.companyName="CalgaryCycling";PlayerSettings.productName="Calgary Cycling Simulator";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"ca.calgarycycling.simulator");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Calgary Cycling Simulator");root.AddComponent<RideApp>().SetSettings(settings);
            EditorSceneManager.SaveScene(scene,"Assets/Cycling/Scenes/Calgary.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Cycling/Scenes/Calgary.unity",true)};
            AssetDatabase.SaveAssets();Selection.activeObject=settings;
            Debug.Log("Scene created. Next: Input System + OpenXR settings, credentials, then Play. Read README.md.");
        }
        [MenuItem("Calgary Cycling/Show Credential Folder")]
        public static void Credentials() { Directory.CreateDirectory(Application.persistentDataPath);EditorUtility.RevealInFinder(Application.persistentDataPath); }
    }
}
```

## Assets/Cycling/Scripts/BicycleMotor.cs

```csharp
using UnityEngine;
namespace CalgaryCycling
{
    public sealed class BicycleMotor : MonoBehaviour
    {
        public RoutePath Route { get; private set; }
        public float Distance { get; private set; }
        public float LateralOffset { get; private set; }
        public float Speed { get; private set; }
        public float Progress => Route == null ? 0 : Distance/Route.Length;
        public bool Arrived => Route != null && Distance >= Route.Length-.001f;
        RideSettings settings;
        float steer, steerVelocity;
        public void Initialize(RoutePath route, RideSettings config) { Route=route;settings=config;ResetRide(); }
        public void ResetRide()
        {
            Distance=LateralOffset=Speed=steer=steerVelocity=0;
            if(Route==null)return;
            transform.localPosition=Route.Position(0);
            transform.localRotation=Quaternion.LookRotation(Route.Forward(0),Vector3.up);
        }
        public void Stop() { Speed=0; steer=steerVelocity=0; }
        public void Tick(float input, float deltaTime)
        {
            if(Route==null || Arrived)return;
            float dt=Mathf.Min(deltaTime,.05f); // avoid teleportation on a long frame
            float remaining=Route.Length-Distance;
            float target=Mathf.Min(settings.cruisingSpeed,Mathf.Sqrt(2*settings.acceleration*remaining));
            Speed=Mathf.MoveTowards(Speed,target,settings.acceleration*dt);
            steer=Mathf.SmoothDamp(steer,Mathf.Clamp(input*settings.steeringSensitivity,-1,1),
                ref steerVelocity,settings.steeringSmoothTime,Mathf.Infinity,dt);
            float angle=steer*settings.maxSteeringAngle*Mathf.Deg2Rad;
            Distance=Mathf.Min(Route.Length,Distance+Speed*Mathf.Cos(angle)*dt);
            // Steering changes actual world/geographic position, not just a handlebar animation.
            // Fade outward motion at the corridor edge. No gravity, collisions or accumulated yaw.
            float edge=1;
            if(Mathf.Sign(steer)==Mathf.Sign(LateralOffset))
                edge=Mathf.Clamp01((settings.corridorHalfWidth-Mathf.Abs(LateralOffset))/.35f);
            LateralOffset=Mathf.Clamp(LateralOffset+Speed*Mathf.Sin(angle)*dt*edge,
                -settings.corridorHalfWidth,settings.corridorHalfWidth);
            Vector3 old=transform.localPosition;
            Vector3 next=Route.Position(Distance,LateralOffset);
            Vector3 motion=next-old;motion.y=0;
            transform.localPosition=next;
            if(motion.sqrMagnitude>.0000001f)
                transform.localRotation=Quaternion.RotateTowards(transform.localRotation,
                    Quaternion.LookRotation(motion,Vector3.up),settings.maxYawDegreesPerSecond*dt);
            if(Route.Length-Distance<.02f) { Distance=Route.Length;Speed=0;transform.localPosition=Route.Position(Distance,LateralOffset); }
        }
    }
}
```

## Assets/Cycling/Scripts/CreditDisplay.cs

```csharp
using CesiumForUnity;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
namespace CalgaryCycling
{
    // Preserve Cesium's own live credit aggregation, images and provider strings.
    // Render its UI Toolkit document to a texture visible in BOTH eyes.
    public sealed class CreditDisplay : MonoBehaviour
    {
        RenderTexture texture;
        PanelSettings panel;
        UIDocument document;
        PanelSettings previous;
        public void Initialize(Camera camera)
        {
            var credits=CesiumCreditSystem.GetDefaultCreditSystem();
            document=credits.GetComponent<UIDocument>();
            if(document==null || document.panelSettings==null)
                throw new System.InvalidOperationException("Cesium credit UIDocument missing; reinstall Cesium 1.18.1.");
            previous=document.panelSettings;
            texture=new RenderTexture(1600,300,0,RenderTextureFormat.ARGB32);
            texture.name="Live map attribution";texture.Create();
            panel=Instantiate(previous);panel.targetTexture=texture;
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.scale=1;
            panel.clearColor=true;panel.colorClearValue=Color.clear;
            credits.gameObject.SetActive(false);
            document.panelSettings=panel;
            credits.gameObject.SetActive(true);
            var canvasGO=new GameObject("Map Attribution",typeof(RectTransform),typeof(Canvas));
            canvasGO.transform.SetParent(camera.transform,false);
            var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            var rect=(RectTransform)canvasGO.transform;
            rect.sizeDelta=new Vector2(1600,350);rect.localScale=Vector3.one*.00135f;
            rect.localPosition=new Vector3(0,-1.1f,2);
            var bg=new GameObject("Background",typeof(RectTransform),typeof(UnityEngine.UI.Image));bg.transform.SetParent(rect,false);
            var br=(RectTransform)bg.transform;br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.sizeDelta=Vector2.zero;
            bg.GetComponent<UnityEngine.UI.Image>().color=new Color(.015f,.025f,.04f,.95f);
            var img=new GameObject("Live provider credits",typeof(RectTransform),typeof(RawImage));img.transform.SetParent(rect,false);
            var ir=(RectTransform)img.transform;ir.sizeDelta=new Vector2(1600,300);ir.anchoredPosition=new Vector2(0,-25);
            img.GetComponent<RawImage>().texture=texture;img.GetComponent<RawImage>().raycastTarget=false;
            var label=new GameObject("Map source",typeof(RectTransform),typeof(Text));label.transform.SetParent(rect,false);
            var lr=(RectTransform)label.transform;lr.sizeDelta=new Vector2(1560,50);lr.anchoredPosition=new Vector2(0,145);
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=23;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;
            text.text="Scenery: Google Maps  |  Bicycle and demo route: Calgary Cycling Simulator";
        }
        void OnDestroy()
        {
            if(document!=null)document.panelSettings=previous;
            if(texture!=null) { texture.Release();Destroy(texture); }
            if(panel!=null)Destroy(panel);
        }
    }
}
```

## Assets/Cycling/Scripts/RideApp.cs

```csharp
using System;
using CesiumForUnity;
using UnityEngine;
namespace CalgaryCycling
{
    public enum RideState { Menu, Riding, Paused, Finished, Error }
    [DefaultExecutionOrder(-100)]
    public sealed class RideApp : MonoBehaviour
    {
        [SerializeField] RideSettings settings;
        public RideSettings Settings { get; private set; }
        public BicycleMotor Motor { get; private set; }
        public RiderCamera Rig { get; private set; }
        public RideInput Input { get; private set; }
        public SceneryService Scenery { get; private set; }
        public RideState State { get; private set; } = RideState.Menu;
        public string Message { get; private set; } = "Welcome. Scenery must load before starting.";
        string fatal;
        public void SetSettings(RideSettings value) { settings=value; }
        void Start()
        {
            try
            {
                if(settings==null)throw new InvalidOperationException("Assign RideSettings on RideApp or run Calgary Cycling > Create Demo Scene.");
                Settings=Instantiate(settings); // Session changes must not dirty the source asset.
                var route=new RoutePath(Settings.waypoints);
                var geoGO=new GameObject("Calgary Georeference");geoGO.transform.SetParent(transform,false);
                var geo=geoGO.AddComponent<CesiumGeoreference>();
                geo.SetOriginLongitudeLatitudeHeight(Settings.longitude,Settings.latitude,Settings.ellipsoidHeight);
                var bike=new GameObject("Bicycle");bike.transform.SetParent(geoGO.transform,false);
                Motor=bike.AddComponent<BicycleMotor>();Motor.Initialize(route,Settings);
                BuildBike(bike.transform);
                if(Settings.showDemoDeck)BuildDeck(geoGO.transform,route);
                var rigGO=new GameObject("XR Origin");rigGO.transform.SetParent(bike.transform,false);
                Rig=rigGO.AddComponent<RiderCamera>();Rig.Initialize(Settings.eyeHeight);
                Input=new RideInput(Settings.useRightStick);
                Scenery=gameObject.AddComponent<SceneryService>();Scenery.Failure+=SceneryFailure;
                Scenery.Initialize(Settings,geoGO.transform);
                gameObject.AddComponent<CreditDisplay>().Initialize(Rig.View);
                gameObject.AddComponent<RideUI>().Initialize(this);
                if(Application.platform!=RuntimePlatform.Android)Application.targetFrameRate=60;
                QualitySettings.vSyncCount=0;
            }
            catch(Exception ex)
            {
                fatal="Setup error: "+ex.Message;State=RideState.Error;Debug.LogError(fatal);
                ShowFatalError();
            }
        }
        void Update()
        {
            if(fatal!=null || Input==null)return;
            if(Input.Menu)Menu();
            else if(Input.Pause) { if(State==RideState.Riding)Pause();else Resume(); }
            if(Input.Restart)Restart();
            if(Input.Recenter)Recenter();
            if(Input.DebugView)Rig.ToggleDebug();
            if(State==RideState.Riding)
            {
                if(!Scenery.Ready) {Pause();return;}
                Motor.Tick(Input.Steering(Settings.deadZone),Time.deltaTime);
                if(Motor.Arrived) {State=RideState.Finished;Motor.Stop();Message="Destination reached. Restart for another ride.";}
            }
        }
        public void StartRide()
        {
            if(!Scenery.Ready || State!=RideState.Menu)return;
            Motor.ResetRide();State=RideState.Riding;Message="Ride in progress.";
        }
        public void Pause()
        {
            if(State!=RideState.Riding)return;
            Motor.Stop();State=RideState.Paused;Message="Paused. Head tracking remains active.";
        }
        public void Resume()
        {
            if(!Scenery.Ready || State!=RideState.Paused)return;
            State=RideState.Riding;Message="Ride in progress.";
        }
        public void Restart()
        {
            if(Motor==null)return;
            Motor.ResetRide();State=Scenery.Ready?RideState.Paused:RideState.Error;
            Message="Ride reset. Choose Resume when ready.";
        }
        public void Menu() {Motor.Stop();State=RideState.Menu;Message="Main menu. Choose Start Ride to begin again.";}
        public void Retry() {Motor.Stop();State=RideState.Menu;Message="Retrying scenery. Start when ready.";Scenery.Retry();}
        void SceneryFailure() {if(Motor!=null)Motor.Stop();State=RideState.Error;Message="Ride stopped because scenery is unavailable.";}
        public void Recenter()
        {
            Pause();Message=Rig.Recenter()?"Recenter requested. Resume when comfortable.":"Use the headset's system recenter command, then Resume.";
        }
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void OnApplicationFocus(bool focus) {if(!focus)Pause();}
        void OnApplicationPause(bool paused) {if(paused)Pause();}
        void OnDestroy() {Input?.Dispose();if(Scenery!=null)Scenery.Failure-=SceneryFailure;if(Settings!=null)Destroy(Settings);}
        void ShowFatalError()
        {
            if(Rig==null)
            {
                var rig=new GameObject("Error XR Origin");rig.transform.SetParent(transform,false);
                Rig=rig.AddComponent<RiderCamera>();Rig.Initialize(1.65f);
            }
            var go=new GameObject("Setup Error",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(Rig.View.transform,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(900,400);
            rect.localPosition=new Vector3(0,0,2);rect.localScale=Vector3.one*.002f;
            var label=new GameObject("Error",typeof(RectTransform),typeof(UnityEngine.UI.Text));
            label.transform.SetParent(rect,false);((RectTransform)label.transform).sizeDelta=rect.sizeDelta;
            var text=label.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=32;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;
            text.text=fatal+"\nExit with the headset system menu. Correct the settings and relaunch.";
        }
        static Material Material(Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.color=color;return m;
        }
        static void Part(Transform parent,string name,PrimitiveType shape,Vector3 p,Vector3 scale,Quaternion rotation,Material mat)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localRotation=rotation;go.transform.localScale=scale;
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        void BuildBike(Transform parent)
        {
            var teal=Material(new Color(.05f,.65f,.65f));var dark=Material(new Color(.025f,.03f,.035f));
            Part(parent,"Front wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark);
            Part(parent,"Rear wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,-.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark);
            Part(parent,"Frame",PrimitiveType.Cube,new Vector3(0,.65f,0),new Vector3(.07f,.08f,1.2f),Quaternion.Euler(-15,0,0),teal);
            Part(parent,"Stem",PrimitiveType.Cube,new Vector3(0,.85f,.6f),new Vector3(.05f,.65f,.05f),Quaternion.identity,teal);
            Part(parent,"Handlebar",PrimitiveType.Cube,new Vector3(0,1.16f,.6f),new Vector3(.65f,.04f,.05f),Quaternion.identity,dark);
            Part(parent,"Saddle",PrimitiveType.Cube,new Vector3(0,.9f,-.35f),new Vector3(.24f,.06f,.32f),Quaternion.identity,dark);
        }
        void BuildDeck(Transform parent,RoutePath route)
        {
            var mat=Material(new Color(.14f,.17f,.19f));
            for(float d=0;d<route.Length;d+=2)
            {
                Vector3 a=route.Position(d),b=route.Position(Mathf.Min(d+2,route.Length));
                Part(parent,"Authored demo deck",PrimitiveType.Cube,(a+b)*.5f-Vector3.up*.08f,
                    new Vector3(Settings.corridorHalfWidth*2+.8f,.15f,Vector3.Distance(a,b)+.1f),
                    Quaternion.LookRotation((b-a).normalized,Vector3.up),mat);
            }
        }
    }
}
```

## Assets/Cycling/Scripts/RideInput.cs

```csharp
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
        readonly InputAction pause, restart, menu, recenter, select, debug;
        public RideInput(bool right)
        {
            keys.AddCompositeBinding("1DAxis").With("Negative","<Keyboard>/a").With("Positive","<Keyboard>/d");
            keys.AddCompositeBinding("1DAxis").With("Negative","<Keyboard>/leftArrow").With("Positive","<Keyboard>/rightArrow");
            stick=new InputAction("Steering",InputActionType.Value,
                right?"<XRController>{RightHand}/thumbstick":"<XRController>{LeftHand}/thumbstick");
            pause=Button("Pause","<Keyboard>/space","<XRController>{LeftHand}/primaryButton");
            restart=Button("Restart","<Keyboard>/r","<XRController>{LeftHand}/secondaryButton");
            menu=Button("Menu","<Keyboard>/escape","<XRController>{RightHand}/secondaryButton");
            recenter=Button("Recenter","<Keyboard>/c","<XRController>{RightHand}/primaryButton");
            select=Button("Select","<XRController>{RightHand}/triggerPressed");
            debug=Button("Third person","<Keyboard>/v");
            keys.Enable();stick.Enable();
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
            return Mathf.Abs(x)<=deadZone?0:Mathf.Sign(x)*Mathf.InverseLerp(deadZone,1,Mathf.Abs(x));
        }
        public bool Pause => pause.WasPressedThisFrame();
        public bool Restart => restart.WasPressedThisFrame();
        public bool Menu => menu.WasPressedThisFrame();
        public bool Recenter => recenter.WasPressedThisFrame();
        public bool Select => select.WasPressedThisFrame();
        public bool DebugView => debug.WasPressedThisFrame();
        public void Dispose()
        {
            keys.Dispose();stick.Dispose();pause.Dispose();restart.Dispose();menu.Dispose();
            recenter.Dispose();select.Dispose();debug.Dispose();
        }
    }
}
```

## Assets/Cycling/Scripts/RideSettings.cs

```csharp
using UnityEngine;
namespace CalgaryCycling
{
    [CreateAssetMenu(menuName = "Calgary Cycling/Ride Settings")]
    public sealed class RideSettings : ScriptableObject
    {
        [Header("WGS84 origin: Peace Bridge, Calgary")]
        public double longitude = -114.07883;
        public double latitude = 51.05389;
        [Tooltip("ELLIPSOID metres, not sea-level height. Initial estimate; calibrate before presenting.")]
        public double ellipsoidHeight = 1040;
        [Header("Authored demonstration path, metres east/up/north from origin")]
        public Vector3[] waypoints = { new Vector3(0,0,-45), new Vector3(0,0,-20),
            new Vector3(2,0,5), new Vector3(6,0,30), new Vector3(12,0,55) };
        [Range(.25f, 2f)] public float corridorHalfWidth = 1.2f;
        [Range(1, 6)] public float cruisingSpeed = 3;
        [Range(.2f, 2)] public float steeringSensitivity = 1;
        [Range(0, .5f)] public float deadZone = .15f;
        [Min(.1f)] public float acceleration = .6f;
        [Min(.1f)] public float steeringSmoothTime = .35f;
        [Range(5, 30)] public float maxSteeringAngle = 12;
        [Range(1, 30)] public float maxYawDegreesPerSecond = 12;
        [Range(1, 2.2f)] public float eyeHeight = 1.65f;
        public bool useRightStick;
        [Header("Streaming budgets: starting points, not performance guarantees")]
        [Range(8, 64)] public float screenSpaceError = 24;
        [Range(64, 512)] public int cacheMiB = 128;
        [Range(1, 16)] public int simultaneousLoads = 4;
        [Range(15, 180)] public float loadTimeout = 90;
        [Header("Synthetic route surface, independent of Google geometry")]
        public bool showDemoDeck = true;
    }
}
```

## Assets/Cycling/Scripts/RideUI.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace CalgaryCycling
{
    public sealed class RideUI : MonoBehaviour
    {
        RideApp app;
        Canvas canvas;
        GraphicRaycaster raycaster;
        InputSystemUIInputModule mouseModule;
        Text status,progress,heading,details;
        GameObject panel,reticle;
        Button start,resume,retry;
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        PointerEventData pointer;
        GameObject hovered;
        Font font;
        float refresh;
        string page="main";
        public void Initialize(RideApp owner)
        {
            app=owner;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var es=new GameObject("Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform,false);mouseModule=es.GetComponent<InputSystemUIInputModule>();
            mouseModule.AssignDefaultActions();pointer=new PointerEventData(es.GetComponent<EventSystem>());
            var go=new GameObject("Ride UI",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            go.transform.SetParent(app.Motor.transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=app.Rig.View;
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(1080,800);
            rect.localScale=Vector3.one*.0024f;rect.localPosition=new Vector3(0,app.Settings.eyeHeight+.15f,3);
            raycaster=go.GetComponent<GraphicRaycaster>();
            panel=new GameObject("Menu",typeof(RectTransform),typeof(Image));panel.transform.SetParent(rect,false);
            var pr=(RectTransform)panel.transform;pr.sizeDelta=rect.sizeDelta;
            panel.GetComponent<Image>().color=new Color(.025f,.055f,.09f,.97f);
            heading=Label(panel.transform,"CALGARY CYCLING SIMULATOR",new Vector2(0,330),new Vector2(1000,60),36);
            status=Label(panel.transform,"Initializing...",new Vector2(0,250),new Vector2(980,90),25);
            start=ButtonAt("Start Ride",-255,150,()=>app.StartRide());
            resume=ButtonAt("Resume",255,150,()=>app.Resume());
            ButtonAt("Restart Ride",-255,65,()=>app.Restart());
            ButtonAt("Main Menu",255,65,()=> {page="main";app.Menu();});
            ButtonAt("Settings",-255,-20,()=> {page="settings";UpdateDetails();});
            ButtonAt("Controller Instructions",255,-20,()=> {page="help";UpdateDetails();});
            ButtonAt("Speed -",-390,-105,()=>ChangeSpeed(-.5f),230);
            ButtonAt("Speed +",-130,-105,()=>ChangeSpeed(.5f),230);
            ButtonAt("Steering -",130,-105,()=>ChangeSensitivity(-.1f),230);
            ButtonAt("Steering +",390,-105,()=>ChangeSensitivity(.1f),230);
            retry=ButtonAt("Retry Scenery",-340,-190,()=>app.Retry(),300);
            ButtonAt("Recenter",0,-190,()=>app.Recenter(),300);
            ButtonAt("Quit",340,-190,()=>app.Quit(),300);
            details=Label(panel.transform,"",new Vector2(0,-305),new Vector2(1000,130),23);
            progress=Label(rect,"",new Vector2(0,465),new Vector2(1080,80),27);
            var pause=MakeButton(rect,"Pause",new Vector2(440,400),new Vector2(190,60),()=>app.Pause());
            pause.name="Pause button";
            reticle=GameObject.CreatePrimitive(PrimitiveType.Sphere);reticle.name="Gaze cursor";
            Destroy(reticle.GetComponent<Collider>());reticle.transform.localScale=Vector3.one*.012f;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.color=Color.cyan;
            reticle.GetComponent<Renderer>().material=mat;reticle.transform.SetParent(transform,false);
            UpdateDetails();
        }
        void ChangeSpeed(float d) {app.Settings.cruisingSpeed=Mathf.Clamp(app.Settings.cruisingSpeed+d,1,6);page="settings";UpdateDetails();}
        void ChangeSensitivity(float d) {app.Settings.steeringSensitivity=Mathf.Clamp(app.Settings.steeringSensitivity+d,.2f,2);page="settings";UpdateDetails();}
        Text Label(Transform parent,string text,Vector2 position,Vector2 size,int fontSize)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchoredPosition=position;r.sizeDelta=size;
            var t=go.GetComponent<Text>();t.font=font;t.text=text;t.color=Color.white;t.fontSize=fontSize;
            t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        Button ButtonAt(string text,float x,float y,Action action,float width=470) =>
            MakeButton(panel.transform,text,new Vector2(x,y),new Vector2(width,65),action);
        Button MakeButton(Transform parent,string text,Vector2 pos,Vector2 size,Action action)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=size;
            go.GetComponent<Image>().color=new Color(.08f,.34f,.4f);
            var b=go.GetComponent<Button>();b.onClick.AddListener(()=>action());
            var colors=b.colors;colors.highlightedColor=new Color(.4f,1,1);colors.disabledColor=new Color(.3f,.3f,.3f);b.colors=colors;
            Label(go.transform,text,Vector2.zero,size,25);return b;
        }
        void UpdateDetails()
        {
            if(details==null)return;
            if(page=="help")details.text="Auto forward. Desktop: A/D or arrows steer; Space pause/resume; R restart; Esc menu; C recenter; V debug view.\nQuest: left stick steer (default); X pause/resume; Y restart; B menu; A recenter. Look at a button and press right trigger.";
            else if(page=="settings")details.text=$"Cruise: {app.Settings.cruisingSpeed:F1} m/s ({app.Settings.cruisingSpeed*3.6f:F1} km/h)   Steering: {app.Settings.steeringSensitivity:F1}\nChanges apply to this session. Edit RideSettings for saved defaults.\nSit comfortably. Stop if motion feels uncomfortable.";
            else details.text="A short guided ride near Peace Bridge, Calgary.\nSteering moves the bicycle sideways within a bounded corridor.\nThe demonstration deck is authored geometry, not a surveyed public cycling route.";
        }
        void Update()
        {
            if(app==null)return;
            bool vr=app.Rig.IsVR;mouseModule.enabled=!vr;reticle.SetActive(vr);
            panel.SetActive(app.State!=RideState.Riding);
            refresh+=Time.unscaledDeltaTime;
            if(refresh>.2f)
            {
                refresh=0;status.text=app.Message+"\n"+app.Scenery.Status;
                progress.text=$"{app.State}  |  {app.Motor.Distance:F0} / {app.Motor.Route.Length:F0} m  |  {app.Motor.Progress:P0}  |  {app.Motor.Speed*3.6f:F1} km/h";
                start.interactable=app.Scenery.Ready && app.State==RideState.Menu;
                resume.interactable=app.Scenery.Ready && app.State==RideState.Paused;
                retry.interactable=app.Scenery.Failed;
            }
            if(!vr)return;
            var camera=app.Rig.View;
            reticle.transform.position=camera.transform.position+camera.transform.forward*2.75f;
            pointer.position=camera.WorldToScreenPoint(camera.transform.position+camera.transform.forward*3);
            hits.Clear();raycaster.Raycast(pointer,hits);
            GameObject target=null;
            foreach(var hit in hits)
            {
                var button=hit.gameObject.GetComponentInParent<Button>();
                if(button!=null && button.IsInteractable()) {target=button.gameObject;break;}
            }
            if(target!=hovered)
            {
                if(hovered!=null)ExecuteEvents.Execute(hovered,pointer,ExecuteEvents.pointerExitHandler);
                hovered=target;
                if(hovered!=null)ExecuteEvents.Execute(hovered,pointer,ExecuteEvents.pointerEnterHandler);
            }
            if(target!=null && app.Input.Select)
                ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
        }
    }
}
```

## Assets/Cycling/Scripts/RiderCamera.cs

```csharp
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
```

## Assets/Cycling/Scripts/RoutePath.cs

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
namespace CalgaryCycling
{
    // A sampled cubic Hermite curve gives continuous tangents at authored waypoints.
    // Distances are along the centreline. No network or input dependency.
    public sealed class RoutePath
    {
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<float> lengths = new List<float>();
        public float Length => lengths.Count == 0 ? 0 : lengths[lengths.Count - 1];
        public RoutePath(Vector3[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2)
                throw new ArgumentException("Route needs at least two waypoints.");
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 p = waypoints[i];
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                    throw new ArgumentException("Route contains a non-finite coordinate.");
                if (i > 0 && Vector3.Distance(p, waypoints[i-1]) < .5f)
                    throw new ArgumentException("Consecutive route points must be at least 0.5 m apart.");
            }
            points.Add(waypoints[0]); lengths.Add(0);
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 a = waypoints[i], b = waypoints[i+1];
                Vector3 m0 = i == 0 ? b-a : (b-waypoints[i-1]) * .5f;
                Vector3 m1 = i+2 == waypoints.Length ? b-a : (waypoints[i+2]-a) * .5f;
                int steps = Mathf.Max(16, Mathf.CeilToInt(Vector3.Distance(a,b) * 4));
                for (int j = 1; j <= steps; j++)
                {
                    float t = (float)j / steps, t2=t*t, t3=t2*t;
                    Vector3 p = (2*t3-3*t2+1)*a + (t3-2*t2+t)*m0 +
                        (-2*t3+3*t2)*b + (t3-t2)*m1;
                    float d=Vector3.Distance(points[points.Count-1],p);
                    if (d < .0001f) continue;
                    lengths.Add(Length+d); points.Add(p);
                }
            }
            if (Length < 2 || Length > 1000)
                throw new ArgumentException("Use a short demo route between 2 and 1000 metres.");
        }
        public Vector3 Position(float distance)
        {
            distance = Mathf.Clamp(distance, 0, Length);
            int lo=0, hi=lengths.Count-1;
            while (hi-lo>1) { int mid=(lo+hi)/2; if(lengths[mid]<distance)lo=mid;else hi=mid; }
            float t=Mathf.InverseLerp(lengths[lo],lengths[hi],distance);
            return Vector3.Lerp(points[lo],points[hi],t);
        }
        public Vector3 Forward(float distance)
        {
            Vector3 v=Position(distance+.6f)-Position(distance-.6f);v.y=0;
            return v.sqrMagnitude < .00001f ? Vector3.forward : v.normalized;
        }
        public Vector3 Right(float distance) => Vector3.Cross(Vector3.up,Forward(distance)).normalized;
        public Vector3 Position(float distance, float offset) => Position(distance)+Right(distance)*offset;
    }
}
```

## Assets/Cycling/Scripts/SceneryService.cs

```csharp
using System;
using System.IO;
using CesiumForUnity;
using UnityEngine;
namespace CalgaryCycling
{
    public sealed class SceneryService : MonoBehaviour
    {
        [Serializable] sealed class Secrets { public string googleMapsApiKey; }
        public Cesium3DTileset Tiles { get; private set; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public string Status { get; private set; } = "Not initialized";
        public float Progress { get; private set; }
        public event Action Failure;
        float elapsed,stable,poll,unready;
        bool sawTile;
        RideSettings settings;
        public void Initialize(RideSettings config, Transform georeference)
        {
            settings=config;
            var go=new GameObject("Google Photorealistic 3D Tiles");go.SetActive(false);
            go.transform.SetParent(georeference,false);
            Tiles=go.AddComponent<Cesium3DTileset>();
            Tiles.tilesetSource=CesiumDataSource.FromUrl;
            Tiles.maximumScreenSpaceError=config.screenSpaceError;
            Tiles.maximumCachedBytes=(long)config.cacheMiB*1024*1024;
            Tiles.maximumSimultaneousTileLoads=(uint)config.simultaneousLoads;
            Tiles.preloadAncestors=false;Tiles.preloadSiblings=false;
            Tiles.createPhysicsMeshes=false;Tiles.showCreditsOnScreen=true;
            Tiles.OnTileGameObjectCreated+=TileCreated;
            Cesium3DTileset.OnCesium3DTilesetLoadFailure+=LoadFailed;
            Retry();
        }
        void TileCreated(GameObject go) { sawTile=true; }
        void LoadFailed(Cesium3DTilesetLoadFailureDetails details)
        {
            if(details.tileset!=Tiles)return;
            // Never display the SDK message: it may contain the credential-bearing URL.
            Fail("Tiles request failed (HTTP "+details.httpStatusCode+"). Check key, billing, quota and connection, then Retry.");
        }
        public void Retry()
        {
            Ready=Failed=sawTile=false;elapsed=stable=poll=unready=0;Progress=0;
            if(Tiles==null)return;
            Tiles.gameObject.SetActive(false);
            string key=Environment.GetEnvironmentVariable("CALGARY_GOOGLE_MAPS_KEY");
            try
            {
                if(string.IsNullOrWhiteSpace(key))
                {
                    string path=Path.Combine(Application.persistentDataPath,"cycling-secrets.json");
                    if(File.Exists(path))key=JsonUtility.FromJson<Secrets>(File.ReadAllText(path))?.googleMapsApiKey;
                }
            }
            catch { Fail("Could not read cycling-secrets.json. Check JSON and file permissions.");return; }
            if(string.IsNullOrWhiteSpace(key)) { Fail("Missing Google Maps key. Configure cycling-secrets.json, then Retry.");return; }
            Tiles.url="https://tile.googleapis.com/v1/3dtiles/root.json?key="+Uri.EscapeDataString(key.Trim());
            Status="Connecting to Google Maps...";
            Tiles.gameObject.SetActive(true);
        }
        void Update()
        {
            if(Tiles==null || Failed || !Tiles.gameObject.activeSelf)return;
            float dt=Time.unscaledDeltaTime;elapsed+=dt;poll+=dt;
            if(Application.internetReachability==NetworkReachability.NotReachable)
            { Fail("No network connection. Reconnect and choose Retry.");return; }
            if(poll<.25f)return;
            float step=poll;poll=0;
            Progress=Mathf.Clamp(Tiles.ComputeLoadProgress()/100f,0,1);
            if(!Ready)
            {
                stable=sawTile && Progress>=.99f ? stable+step : 0;
                Status="Loading current view: "+Mathf.RoundToInt(Progress*100)+"%";
                if(stable>=2) { Ready=true;Status="Scenery ready. Inspect route alignment before presenting."; }
                else if(elapsed>settings.loadTimeout)Fail("Scenery loading timed out. Check network, key, quota or coverage, then Retry.");
            }
            else
            {
                unready=Progress<.90f?unready+step:0;
                Status=Progress<.99f?"Streaming scenery: "+Mathf.RoundToInt(Progress*100)+"%":"Scenery ready";
                if(unready>settings.loadTimeout)Fail("Streaming stalled. Ride paused; reconnect and Retry.");
            }
        }
        void Fail(string message) { Ready=false;Failed=true;Status=message;Failure?.Invoke(); }
        void OnDestroy()
        {
            Cesium3DTileset.OnCesium3DTilesetLoadFailure-=LoadFailed;
            if(Tiles!=null)Tiles.OnTileGameObjectCreated-=TileCreated;
        }
    }
}
```
