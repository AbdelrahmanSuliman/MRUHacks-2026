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
        RoutePlannerUI plannerUI;
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
            panel.GetComponent<Image>().color=new Color(.018f,.055f,.065f,.98f);
            heading=Label(panel.transform,"CALGARY  /  RIDE",new Vector2(0,330),new Vector2(1000,60),38);heading.color=new Color(.45f,1f,.78f);
            status=Label(panel.transform,"Initializing...",new Vector2(0,250),new Vector2(980,90),25);
            start=ButtonAt("Start Ride",-255,150,()=>app.StartRide());
            resume=ButtonAt("Resume",255,150,()=>app.Resume());
            ButtonAt("Restart Ride",-255,65,()=>app.Restart());
            ButtonAt("Main Menu",255,65,()=> {page="main";app.Menu();});
            ButtonAt("Settings",-255,-20,()=> {page="settings";UpdateDetails();});
            ButtonAt("Controller Instructions",255,-20,()=> {page="help";UpdateDetails();});
            ButtonAt("PLAN ROUTE",0,-20,()=> {plannerUI?.Show();},300);
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
            plannerUI=app.GetComponent<RoutePlannerUI>();
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
