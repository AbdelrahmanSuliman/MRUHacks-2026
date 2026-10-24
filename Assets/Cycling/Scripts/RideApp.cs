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
        public RoutePlannerService Planner { get; private set; }
        Transform georeference;
        Transform frontWheel, rearWheel, handlebar;
        public RideState State { get; private set; } = RideState.Menu;
        public string Message { get; private set; } = "Welcome. Scenery must load before starting.";
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
                BuildBike(bike.transform);
                if(Settings.showDemoDeck){BuildDeck(geoGO.transform,route);BuildDemoVegetation(geoGO.transform,route);BuildDemoBuildings(geoGO.transform,route);}
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
                RenderSettings.ambientLight=new Color(.58f,.68f,.78f);
                RenderSettings.fog=true;RenderSettings.fogColor=new Color(.48f,.65f,.78f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=80;RenderSettings.fogEndDistance=420;
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
                if(!Scenery.Ready && !Scenery.Failed) {Pause();return;}
                Motor.Tick(Input.Steering(Settings.deadZone),Time.deltaTime);
                AnimateBike();
                if(Motor.Arrived) {State=RideState.Finished;Motor.Stop();Message="Destination reached. Restart for another ride.";}
            }
        }
        public void StartRide()
        {
            if((!Scenery.Ready && !Scenery.Failed) || State!=RideState.Menu)return;
            Motor.ResetRide();State=RideState.Riding;Message="Ride in progress.";
        }
        public void Pause()
        {
            if(State!=RideState.Riding)return;
            Motor.Stop();State=RideState.Paused;Message="Paused. Head tracking remains active.";
        }
        public void Resume()
        {
            if((!Scenery.Ready && !Scenery.Failed) || State!=RideState.Paused)return;
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
        public void ApplyOnlineRoute(RoutePlannerService.RouteResult result)
        {
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
                var route=new RoutePath(points);Motor.Initialize(route,Settings);State=RideState.Menu;
                Message="Route loaded. Confirm scenery is ready, then start your ride.";
            }
            catch(Exception ex){Debug.LogError("[RideApp] Route conversion failed: "+ex.Message);Message="Route was too long or invalid.";}
        }
        void SceneryFailure() {if(Motor!=null)Motor.Stop();State=RideState.Menu;Message="Offline demo scenery active. Streaming scenery is unavailable.";}
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
        static GameObject Part(Transform parent,string name,PrimitiveType shape,Vector3 p,Vector3 scale,Quaternion rotation,Material mat)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localRotation=rotation;go.transform.localScale=scale;
            Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go;
        }
        void BuildBike(Transform parent)
        {
            var teal=Material(new Color(.05f,.65f,.65f));var dark=Material(new Color(.025f,.03f,.035f));
            frontWheel=Part(parent,"Front wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark).transform;
            rearWheel=Part(parent,"Rear wheel",PrimitiveType.Cylinder,new Vector3(0,.34f,-.65f),new Vector3(.68f,.035f,.68f),Quaternion.Euler(0,0,90),dark).transform;
            Part(parent,"Frame",PrimitiveType.Cube,new Vector3(0,.65f,0),new Vector3(.07f,.08f,1.2f),Quaternion.Euler(-15,0,0),teal);
            Part(parent,"Stem",PrimitiveType.Cube,new Vector3(0,.85f,.6f),new Vector3(.05f,.65f,.05f),Quaternion.identity,teal);
            handlebar=Part(parent,"Handlebar",PrimitiveType.Cube,new Vector3(0,1.16f,.6f),new Vector3(.65f,.04f,.05f),Quaternion.identity,dark).transform;
            Part(parent,"Saddle",PrimitiveType.Cube,new Vector3(0,.9f,-.35f),new Vector3(.24f,.06f,.32f),Quaternion.identity,dark);
        }
        void AnimateBike()
        {
            float spin=Motor.Speed*Time.deltaTime*360f;
            if(frontWheel!=null)frontWheel.Rotate(Vector3.forward,spin,Space.Self);
            if(rearWheel!=null)rearWheel.Rotate(Vector3.forward,spin,Space.Self);
            if(handlebar!=null)handlebar.localRotation=Quaternion.Euler(0,Input.Steering(Settings.deadZone)*18f,0);
        }
        void BuildDeck(Transform parent,RoutePath route)
        {
            var mat=Material(new Color(.14f,.17f,.19f));
            var edge=Material(new Color(.04f,.75f,.82f));var arrow=Material(new Color(.2f,.95f,.5f));
            for(float d=0;d<route.Length;d+=2)
            {
                Vector3 a=route.Position(d),b=route.Position(Mathf.Min(d+2,route.Length));
                Part(parent,"Authored demo deck",PrimitiveType.Cube,(a+b)*.5f-Vector3.up*.08f,
                    new Vector3(Settings.corridorHalfWidth*2+.8f,.15f,Vector3.Distance(a,b)+.1f),
                    Quaternion.LookRotation((b-a).normalized,Vector3.up),mat);
            }
            for(float d=6;d<route.Length;d+=12)
            {
                Vector3 p=route.Position(d)+Vector3.up*.11f;Vector3 f=route.Forward(d);
                Part(parent,"Cyan route edge L",PrimitiveType.Cube,route.Position(d)+route.Right(d)*(Settings.corridorHalfWidth+.18f)+Vector3.up*.04f,new Vector3(.06f,.025f,1.1f),Quaternion.LookRotation(f,Vector3.up),edge);
                Part(parent,"Cyan route edge R",PrimitiveType.Cube,route.Position(d)-route.Right(d)*(Settings.corridorHalfWidth+.18f)+Vector3.up*.04f,new Vector3(.06f,.025f,1.1f),Quaternion.LookRotation(f,Vector3.up),edge);
                Part(parent,"Green route arrow",PrimitiveType.Cube,p+f*.35f,new Vector3(.18f,.035f,.7f),Quaternion.LookRotation(f,Vector3.up),arrow);
            }
        }
        void BuildDemoVegetation(Transform parent,RoutePath route)
        {
            var trunk=Material(new Color(.18f,.09f,.035f));var leaves=Material(new Color(.05f,.28f,.12f));
            for(float d=8;d<route.Length;d+=16)
            {
                Vector3 c=route.Position(d);Vector3 r=route.Right(d);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 p=c+r*side*(Settings.corridorHalfWidth+3.5f);Part(parent,"Demo tree trunk",PrimitiveType.Cylinder,p+Vector3.up*1.1f,new Vector3(.18f,1.1f,.18f),Quaternion.identity,trunk);Part(parent,"Demo tree canopy",PrimitiveType.Sphere,p+Vector3.up*2.5f,new Vector3(1.3f,1.6f,1.3f),Quaternion.identity,leaves);
                }
            }
        }
        void BuildDemoBuildings(Transform parent,RoutePath route)
        {
            var walls=new[]{Material(new Color(.28f,.38f,.48f)),Material(new Color(.42f,.31f,.25f)),Material(new Color(.24f,.46f,.48f))};
            var roofs=Material(new Color(.06f,.10f,.14f));
            for(float d=18;d<route.Length;d+=28)
            {
                Vector3 c=route.Position(d);Vector3 r=route.Right(d);Vector3 f=route.Forward(d);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 p=c+r*side*(Settings.corridorHalfWidth+8f);float h=3.5f+(Mathf.Abs(Mathf.Sin(d*.13f))*4f);float w=4f+(Mathf.Abs(Mathf.Cos(d*.07f))*4f);
                    Part(parent,"Stylized city building",PrimitiveType.Cube,p+Vector3.up*(h*.5f),new Vector3(w,h,6f),Quaternion.LookRotation(f,Vector3.up),walls[Mathf.Abs(Mathf.RoundToInt(d))%walls.Length]);
                    Part(parent,"Building roof",PrimitiveType.Cube,p+Vector3.up*(h+.12f),new Vector3(w+.15f,.18f,6.15f),Quaternion.LookRotation(f,Vector3.up),roofs);
                }
            }
        }
    }
}
