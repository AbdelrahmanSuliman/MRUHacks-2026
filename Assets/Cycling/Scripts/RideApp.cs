using System;
using CesiumForUnity;
using UnityEngine;
using UnityEngine.Rendering;
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
        public RoutePlannerService Planner { get; private set; }
        Transform georeference;
        StylizedEnvironmentBuilder stylizedEnvironment;
        Transform frontWheel, rearWheel, handlebar;
        Quaternion handlebarRestRotation;
        float visualSteering;
        public RideState State { get; private set; } = RideState.Menu;
        public string Message { get; private set; } = "Welcome. Choose your ride.";
        public bool CanRide=>Scenery!=null && (Scenery.Ready || Scenery.Failed);
        string fatal;
        public void SetSettings(RideSettings value) { settings=value; }
        public void SetMessage(string value) { Message=value; }
        void Start()
        {
            try
            {
                if(settings==null)throw new InvalidOperationException("Assign RideSettings on RideApp or run Calgary Cycling > Create Demo Scene.");
                Settings=Instantiate(settings); // Session changes must not dirty the source asset.
                var route=new RoutePath(Settings.waypoints);
                var geoGO=new GameObject("Calgary Georeference");geoGO.transform.SetParent(transform,false);georeference=geoGO.transform;
                var geo=geoGO.AddComponent<CesiumGeoreference>();
                geo.SetOriginLongitudeLatitudeHeight(Settings.longitude,Settings.latitude,Settings.ellipsoidHeight);
                var bike=new GameObject("Bicycle");bike.transform.SetParent(geoGO.transform,false);
                Motor=bike.AddComponent<BicycleMotor>();Motor.Initialize(route,Settings);
                if (Settings.bicyclePrefab != null)
                {
                    var importedBike=Instantiate(Settings.bicyclePrefab,bike.transform);
                    importedBike.name="Imported stylized bicycle";
                    frontWheel=FindMount(importedBike.transform,"FrontWheel","Front Wheel");
                    rearWheel=FindMount(importedBike.transform,"RearWheel","Rear Wheel");
                    handlebar=FindMount(importedBike.transform,"HandlebarRoot","Handlebar");
                }
                else BuildBike(bike.transform);
                if(handlebar!=null)handlebarRestRotation=handlebar.localRotation;
                stylizedEnvironment = new StylizedEnvironmentBuilder(Settings, geoGO.transform);
                if (Settings.showDemoDeck || Settings.stylizedAssets != null)
                    stylizedEnvironment.Build(route, Settings.stylizedZone, true);
                var rigGO=new GameObject("XR Origin");rigGO.transform.SetParent(bike.transform,false);
                Rig=rigGO.AddComponent<RiderCamera>();Rig.Initialize(Settings.eyeHeight);
                Input=new RideInput(Settings.useRightStick);
                Scenery=gameObject.AddComponent<SceneryService>();Scenery.Failure+=SceneryFailure;
                Scenery.Initialize(Settings,geoGO.transform);
                Planner=gameObject.AddComponent<RoutePlannerService>();
                var addressPlanner=gameObject.AddComponent<RoutePlannerUI>();
                addressPlanner.Initialize(this,Planner);
                gameObject.AddComponent<CreditDisplay>().Initialize(Rig.View);
                gameObject.AddComponent<VeloUIManager>().Initialize(this);
                if(Application.platform!=RuntimePlatform.Android)Application.targetFrameRate=60;
                QualitySettings.vSyncCount=0;
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.42f,.64f,.88f);
                RenderSettings.ambientEquatorColor=new Color(.78f,.84f,.72f);
                RenderSettings.ambientGroundColor=new Color(.28f,.36f,.24f);
                RenderSettings.ambientIntensity=1.15f;
                RenderSettings.fog=true;RenderSettings.fogColor=new Color(.58f,.75f,.86f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=500;
                ConfigureStylizedLighting();
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
            Input.Tick();
            if(Input.Menu)Menu();
            else if(Input.Pause) { if(State==RideState.Riding)Pause();else Resume(); }
            if(Input.Restart)Restart();
            if(Input.Recenter)Recenter();
            if(Input.DebugView)Rig.ToggleDebug();
            if(State==RideState.Riding)
            {
                if(!Scenery.Ready && !Scenery.Failed) {Pause();return;}
                float steering=Input.Steering(Settings.deadZone);
                Motor.Tick(steering,Time.deltaTime);
                AnimateBike();
                if(Motor.Arrived) {State=RideState.Finished;Motor.Stop();Message="Destination reached. Restart for another ride.";}
            }
        }
        public void StartRide()
        {
            if(!CanRide || State!=RideState.Menu)return;
            Input.CalibrateSteering();Motor.ResetRide();State=RideState.Riding;Message="Ride in progress.";
            Debug.Log("[RideApp] Ride started.");
        }
        public void Pause()
        {
            if(State!=RideState.Riding)return;
            Motor.Stop();State=RideState.Paused;Message="Paused. Head tracking remains active.";
        }
        public void Resume()
        {
            if(!CanRide || State!=RideState.Paused)return;
            Input.CalibrateSteering();State=RideState.Riding;Message="Ride in progress.";
        }
        public void Restart()
        {
            if(Motor==null)return;
            Motor.ResetRide();State=CanRide?RideState.Paused:RideState.Menu;
            Message="Ride reset. Choose Resume when ready.";
        }
        public void Menu() {Motor.Stop();State=RideState.Menu;Message="Main menu. Choose Start Ride to begin again.";}
        public void Retry() {Motor.Stop();State=RideState.Menu;Message="Retrying scenery. Start when ready.";Scenery.Retry();}
        public bool ApplyOnlineRoute(RoutePlannerService.RouteResult result)
        {
            if(result==null || result.points==null || result.points.Count<2)
            {
                Message="Route geometry was empty or invalid.";
                return false;
            }
            var points=new Vector3[result.points.Count];
            double lat0=Settings.latitude*Math.PI/180.0;
            for(int i=0;i<points.Length;i++)
            {
                var p=result.points[i];
                points[i]=new Vector3((float)((p.longitude-Settings.longitude)*111320.0*Math.Cos(lat0)),0,
                    (float)((p.latitude-Settings.latitude)*110540.0));
            }
            try
            {
                var route=new RoutePath(points,false);Motor.Initialize(route,Settings);State=RideState.Menu;
                stylizedEnvironment?.Build(route, Settings.stylizedZone, true);
                Message=$"Cycling route loaded: {route.Length/1000f:F1} km. Start when scenery is ready.";
                return true;
            }
            catch(Exception ex){Debug.LogError("[RideApp] Route conversion failed: "+ex.Message);Message="Route was too long or invalid.";return false;}
        }
        void SceneryFailure() {if(Motor!=null)Motor.Stop();State=RideState.Menu;Message="Offline demo scenery active. Streaming scenery is unavailable.";}
        public void Recenter()
        {
            Input?.CalibrateSteering();
            Pause();Message=Rig.Recenter()?"Recenter requested. Resume when comfortable.":"Use the headset's system recenter command, then Resume.";
        }
        public void CalibrateHandlebars()
        {
            Input?.CalibrateSteering();
            Message="Hold both grips with your hands straight ahead to centre the handlebars.";
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
        void OnDestroy()
        {
            Input?.Dispose();stylizedEnvironment?.Dispose();if(Scenery!=null)Scenery.Failure-=SceneryFailure;
            if(Settings!=null){if(Application.isPlaying)Destroy(Settings);else DestroyImmediate(Settings);}
        }
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
        static void ConfigureStylizedLighting()
        {
            var sun=RenderSettings.sun;
            if(sun==null)
            {
                var go=new GameObject("Warm Alpine Sun");sun=go.AddComponent<Light>();sun.type=LightType.Directional;RenderSettings.sun=sun;
            }
            sun.color=new Color(1f,.73f,.48f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;
            sun.transform.rotation=Quaternion.Euler(38f,-28f,0f);
            var sky=RenderSettings.skybox;
            if(sky==null || sky.shader==null || !sky.shader.name.Contains("Skybox"))
            {
                sky=new Material(Shader.Find("Skybox/Procedural"));
                RenderSettings.skybox=sky;
            }
            if(sky!=null && sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint",new Color(.28f,.55f,.9f));
            if(sky!=null && sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor",new Color(.72f,.78f,.66f));
            if(sky!=null && sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize",.04f);
            DynamicGI.UpdateEnvironment();
        }
        static GameObject Part(Transform parent,string name,PrimitiveType shape,Vector3 p,Vector3 scale,Quaternion rotation,Material mat)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localRotation=rotation;go.transform.localScale=scale;
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        void BuildBike(Transform parent)
        {
            var teal=Material(new Color(.05f,.65f,.65f));var dark=Material(new Color(.04f,.09f,.10f));var yellow=Material(new Color(.96f,.66f,.12f));
            frontWheel=Part(parent,"Front wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark).transform;
            rearWheel=Part(parent,"Rear wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,-.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark).transform;
            Part(parent,"Frame",PrimitiveType.Cube,new Vector3(0,.65f,0),new Vector3(.07f,.08f,1.2f),Quaternion.Euler(-15,0,0),teal);
            Part(parent,"Stem",PrimitiveType.Cube,new Vector3(0,1.05f,.825f),new Vector3(.045f,.54f,.045f),Quaternion.FromToRotation(Vector3.up,new Vector3(0,.3f,.45f)),teal);
            handlebar=new GameObject("HandlebarRoot").transform;handlebar.SetParent(parent,false);
            handlebar.localPosition=new Vector3(0,1.20f,1.05f);
            Part(handlebar,"Warm alloy handlebar",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.035f,.34f,.035f),Quaternion.Euler(0,0,90),yellow);
            Part(handlebar,"Left rubber grip",PrimitiveType.Cylinder,new Vector3(-.29f,0,0),new Vector3(.048f,.075f,.048f),Quaternion.Euler(0,0,90),dark);
            Part(handlebar,"Right rubber grip",PrimitiveType.Cylinder,new Vector3(.29f,0,0),new Vector3(.048f,.075f,.048f),Quaternion.Euler(0,0,90),dark);
            Part(handlebar,"Left brake lever",PrimitiveType.Cube,new Vector3(-.24f,-.025f,.07f),new Vector3(.12f,.025f,.035f),Quaternion.Euler(0,20,0),dark);
            Part(handlebar,"Right brake lever",PrimitiveType.Cube,new Vector3(.24f,-.025f,.07f),new Vector3(.12f,.025f,.035f),Quaternion.Euler(0,-20,0),dark);
            Part(parent,"Saddle",PrimitiveType.Cube,new Vector3(0,.9f,-.35f),new Vector3(.3f,.08f,.38f),Quaternion.identity,dark);
        }
        static Transform FindMount(Transform root,params string[] names)
        {
            foreach(var name in names)
            {
                var child=FindRecursive(root,name);
                if(child!=null)return child;
            }
            return null;
        }
        static Transform FindRecursive(Transform root,string name)
        {
            if(root.name.Equals(name,StringComparison.OrdinalIgnoreCase))return root;
            for(int i=0;i<root.childCount;i++){var found=FindRecursive(root.GetChild(i),name);if(found!=null)return found;}
            return null;
        }
        void AnimateBike()
        {
            float spin=Motor.Speed*Time.deltaTime*360f;
            if(frontWheel!=null)frontWheel.Rotate(Vector3.forward,spin,Space.Self);
            if(rearWheel!=null)rearWheel.Rotate(Vector3.forward,spin,Space.Self);
            visualSteering=Mathf.Lerp(visualSteering,Input.CurrentSteering,1-Mathf.Exp(-8f*Time.deltaTime));
            if(handlebar!=null)handlebar.localRotation=handlebarRestRotation*Quaternion.Euler(0,visualSteering*25f,0);
        }
    }
}
