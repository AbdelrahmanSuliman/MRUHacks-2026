using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CalgaryCycling
{
    public sealed class RoutePlannerUI : MonoBehaviour
    {
        RideApp app; RoutePlannerService planner; Canvas canvas; Text message,stats; InputField start,end; Button searchStart,searchEnd,route; bool searchingStart=true;
        readonly List<RoutePlannerService.Place> starts=new List<RoutePlannerService.Place>(), ends=new List<RoutePlannerService.Place>();
        Font font;
        bool open;
        public bool IsOpen => open;
        public void Show() { open=true; canvas.gameObject.SetActive(true); }
        public void Hide() { open=false; if(canvas!=null)canvas.gameObject.SetActive(false); }
        public void Initialize(RideApp owner,RoutePlannerService service)
        {
            app=owner;planner=service;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            planner.PlacesFound+=PlacesFound;planner.RouteFound+=RouteFound;planner.Failed+=Failed;
            var go=new GameObject("Fitness Route Planner",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(app.Motor.transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=app.Rig.View;
            var root=(RectTransform)go.transform;root.sizeDelta=new Vector2(1080,800);root.localScale=Vector3.one*.0024f;root.localPosition=new Vector3(0,app.Settings.eyeHeight+.15f,3.15f);
            var bg=new GameObject("Planner Card",typeof(RectTransform),typeof(Image));bg.transform.SetParent(root,false);bg.GetComponent<Image>().color=new Color(.035f,.08f,.10f,.98f);((RectTransform)bg.transform).sizeDelta=root.sizeDelta;
            Label(root,"PLAN YOUR RIDE",new Vector2(0,330),new Vector2(1000,60),38,Color.white);
            Label(root,"Choose two Calgary addresses and follow a continuous cycling route.",new Vector2(0,280),new Vector2(1000,40),20,new Color(.55f,1,.9f));
            start=Field(root,"START ADDRESS",new Vector2(-160,190));end=Field(root,"FINISH ADDRESS",new Vector2(-160,90));
            searchStart=Button(root,"FIND START",new Vector2(330,190),()=>{searchingStart=true;planner.SearchCalgary(start.text);});
            searchEnd=Button(root,"FIND FINISH",new Vector2(330,90),()=>{searchingStart=false;planner.SearchCalgary(end.text);});
            route=Button(root,"CALCULATE CYCLING ROUTE",new Vector2(0,-70),()=>{if(starts.Count>0&&ends.Count>0)planner.BicycleRoute(starts[0],ends[0]);});
            message=Label(root,"Search for both addresses to begin.",new Vector2(0,-155),new Vector2(950,50),22,Color.white);
            stats=Label(root,"",new Vector2(0,-225),new Vector2(950,100),24,new Color(.75f,1,.9f));
            Button(root,"BACK TO MENU",new Vector2(0,-320),Hide);
            canvas.gameObject.SetActive(false);
        }
        InputField Field(Transform p,string hint,Vector2 pos){var go=new GameObject(hint,typeof(RectTransform),typeof(Image),typeof(InputField));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=new Vector2(610,70);go.GetComponent<Image>().color=new Color(.1f,.18f,.2f);var input=go.GetComponent<InputField>();input.shouldHideMobileInput=false;input.lineType=InputField.LineType.SingleLine;input.textComponent=Label(go.transform,"",Vector2.zero,r.sizeDelta,23,Color.white);input.placeholder=Label(go.transform,hint,Vector2.zero,r.sizeDelta,21,new Color(.55f,.65f,.65f));return input;}
        Button Button(Transform p,string text,Vector2 pos,System.Action action){var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=new Vector2(360,70);go.GetComponent<Image>().color=new Color(.05f,.55f,.45f);var b=go.GetComponent<Button>();b.onClick.AddListener(()=>action());Label(go.transform,text,Vector2.zero,r.sizeDelta,20,Color.white);return b;}
        Text Label(Transform p,string value,Vector2 pos,Vector2 size,int sizePx,Color color){var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=size;var t=go.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=sizePx;t.color=color;t.alignment=TextAnchor.MiddleCenter;return t;}
        void PlacesFound(List<RoutePlannerService.Place> places){if(message==null)return;if(searchingStart){starts.Clear();starts.AddRange(places);message.text="START READY  •  "+places[0].label+"\nNow find your finish address.";}else{ends.Clear();ends.AddRange(places);message.text="FINISH READY  •  "+places[0].label+"\nCalculate your cycling route.";}}
        void RouteFound(RoutePlannerService.RouteResult result)
        {
            app.ApplyOnlineRoute(result);
            stats.text=$"ROUTE READY\n{result.distance/1000f:F1} km  |  {result.duration/60f:F0} min estimated\nThe route is loaded. Return to the dashboard to start.";
            Hide();
        }
        void Failed(string error){message.text=error;}
        void Update(){if(app==null||canvas==null)return;if(!open)canvas.gameObject.SetActive(false);}
        void OnDestroy(){if(planner!=null){planner.PlacesFound-=PlacesFound;planner.RouteFound-=RouteFound;planner.Failed-=Failed;}}
    }
}
