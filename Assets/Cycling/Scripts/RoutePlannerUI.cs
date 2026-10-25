using System.Collections.Generic;
using CalgaryCycling.RouteUI;
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
        RectTransform routeRoot, onlineRoot;
        RoutePath demoPath;
        Button demoCard, previewCard, startSelected;
        Text demoText, previewText;
        bool demoSelected=true;
        OfflineRouteSummary offlineSummary;
        public bool CanStartSelected => demoSelected && ReferenceEquals(app.Motor.Route,demoPath) &&
            demoPath!=null && app.State==RideState.Menu && (app.Scenery.Ready || app.Scenery.Failed);

        public void StartSelectedRide()
        {
            if(!CanStartSelected) { Show(); return; }
            app.StartRide();
            if(app.State==RideState.Riding)Close();
        }

        void ShowPredefinedRoutes()
        {
            onlineRoot.gameObject.SetActive(false);
            routeRoot.gameObject.SetActive(true);
            RefreshCards();
        }

        void RefreshCards()
        {
            bool active=ReferenceEquals(app.Motor.Route,demoPath);
            float duration=app.Settings.cruisingSpeed>0?demoPath.Length/app.Settings.cruisingSpeed:0;
            demoText.text=$"{(demoSelected?"SELECTED  •  ":"")}PEACE BRIDGE DEMO\n{demoPath.Length:F0} m  |  ~{duration:F0} sec at cruise speed\n"+
                (active?"RIDEABLE • Authored demonstration path":"UNAVAILABLE • Active path changed; restart Play to restore demo");
            previewText.text=offlineSummary==null?"SAVED CALGARY ROUTE\nUNAVAILABLE • Missing or invalid route data":
                $"{(!demoSelected?"SELECTED  •  ":"")}SAVED CALGARY ROUTE\n{offlineSummary.distanceMeters/1000f:F2} km  |  {offlineSummary.durationSeconds/60f:F1} min\nPREVIEW ONLY • Bicycle geometry integration required";
            demoCard.GetComponent<Image>().color=demoSelected?new Color(.05f,.5f,.4f):new Color(.1f,.18f,.2f);
            previewCard.GetComponent<Image>().color=!demoSelected?new Color(.05f,.5f,.4f):new Color(.1f,.18f,.2f);
            previewCard.interactable=offlineSummary!=null;
            startSelected.interactable=CanStartSelected;
        }
        public bool IsOpen => canvas!=null && canvas.gameObject.activeSelf;
        public GraphicRaycaster Raycaster => canvas.GetComponent<GraphicRaycaster>();
        public void Close() { open=false; if(canvas!=null)canvas.gameObject.SetActive(false); }
        public void Show() { open=true; canvas.gameObject.SetActive(true);ShowPredefinedRoutes(); }
        public void Initialize(RideApp owner,RoutePlannerService service)
        {
            app=owner;planner=service;demoPath=app.Motor.Route;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            planner.PlacesFound+=PlacesFound;planner.RouteFound+=RouteFound;planner.Failed+=Failed;
            var go=new GameObject("Fitness Route Planner",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(app.Motor.transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=app.Rig.View;
            var root=(RectTransform)go.transform;root.sizeDelta=new Vector2(1080,800);root.localScale=Vector3.one*.0024f;root.localPosition=new Vector3(0,app.Settings.eyeHeight+.15f,3.15f);
            var bg=new GameObject("Planner Card",typeof(RectTransform),typeof(Image));bg.transform.SetParent(root,false);bg.GetComponent<Image>().color=new Color(.035f,.08f,.10f,.98f);((RectTransform)bg.transform).sizeDelta=root.sizeDelta;
            var onlineGO=new GameObject("Online Routing",typeof(RectTransform));
            onlineGO.transform.SetParent(root,false);
            onlineRoot=(RectTransform)onlineGO.transform;onlineRoot.sizeDelta=root.sizeDelta;
            var routeGO=new GameObject("Predefined Routes",typeof(RectTransform));
            routeGO.transform.SetParent(root,false);
            routeRoot=(RectTransform)routeGO.transform;routeRoot.sizeDelta=root.sizeDelta;
            var offline=Resources.Load<TextAsset>("RouteUI/OfflineRoute");
            if(offline!=null)OfflineRouteSummary.TryParse(offline.text,out offlineSummary);
            BuildPredefinedRoutes();
            var onlinePanel=onlineRoot;
            Label(onlinePanel,"PLAN YOUR RIDE",new Vector2(0,330),new Vector2(1000,60),38,Color.white);
            Label(onlinePanel,"Preview a saved route, or search online for Calgary cycling directions.",new Vector2(0,280),new Vector2(1000,40),20,new Color(.55f,1,.9f));
            start=Field(onlinePanel,"START ADDRESS",new Vector2(-160,190));end=Field(onlinePanel,"FINISH ADDRESS",new Vector2(-160,90));
            searchStart=Button(onlinePanel,"FIND START",new Vector2(330,190),()=>{searchingStart=true;planner.SearchCalgary(start.text);});
            searchEnd=Button(onlinePanel,"FIND FINISH",new Vector2(330,90),()=>{searchingStart=false;planner.SearchCalgary(end.text);});
            route=Button(onlinePanel,"CALCULATE CYCLING ROUTE",new Vector2(-245,-15),()=>{if(starts.Count>0&&ends.Count>0)planner.BicycleRoute(starts[0],ends[0]);});
            Button(onlinePanel,"PREVIEW OFFLINE ROUTE",new Vector2(245,-15),ShowOfflinePreview);
            message=Label(onlinePanel,"Search for both addresses to begin.",new Vector2(0,-100),new Vector2(950,65),20,Color.white);
            stats=Label(onlinePanel,"",new Vector2(0,-215),new Vector2(1000,140),20,new Color(.75f,1,.9f));
            Button(onlinePanel,"BACK TO ROUTES",new Vector2(0,-320),ShowPredefinedRoutes);
            ShowOfflinePreview();
            ShowPredefinedRoutes();
            Close();
        }
        void BuildPredefinedRoutes()
        {
            Label(routeRoot,"SELECT YOUR RIDE",new Vector2(0,330),new Vector2(1000,60),38,Color.white);
            Label(routeRoot,"Choose a route. Only integrated paths can start a ride.",new Vector2(0,275),new Vector2(1000,45),22,Color.white);
            demoCard=Button(routeRoot,"",new Vector2(0,155),()=>{demoSelected=true;RefreshCards();});
            demoCard.GetComponent<RectTransform>().sizeDelta=new Vector2(950,140);
            demoText=demoCard.GetComponentInChildren<Text>();demoText.rectTransform.sizeDelta=new Vector2(920,130);
            previewCard=Button(routeRoot,"",new Vector2(0,-15),()=>{demoSelected=false;RefreshCards();});
            previewCard.GetComponent<RectTransform>().sizeDelta=new Vector2(950,140);
            previewText=previewCard.GetComponentInChildren<Text>();previewText.rectTransform.sizeDelta=new Vector2(920,130);
            Label(routeRoot,"Saved route selection is a preview and never changes the bicycle path.",new Vector2(0,-135),new Vector2(1000,60),20,Color.white);
            startSelected=Button(routeRoot,"START RIDE",new Vector2(0,-215),StartSelectedRide);
            Button(routeRoot,"CLOSE",new Vector2(-245,-320),Close);
            Button(routeRoot,"ONLINE ROUTING",new Vector2(245,-320),()=>{
                routeRoot.gameObject.SetActive(false);onlineRoot.gameObject.SetActive(true);
            });
        }

        void ShowOfflinePreview()
        {
            var asset=Resources.Load<TextAsset>("RouteUI/OfflineRoute");
            if(asset==null || !OfflineRouteSummary.TryParse(asset.text,out var summary))
            {
                stats.text="Offline route unavailable or invalid.";
                message.text="Online routing remains available.";
                return;
            }
            stats.text=summary.PreviewText;
            message.text="Preview only. Return to routes and select the demo to ride.\nThe saved route has not been applied to the bicycle.";
        }
        InputField Field(Transform p,string hint,Vector2 pos){var go=new GameObject(hint,typeof(RectTransform),typeof(Image),typeof(InputField));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=new Vector2(610,70);go.GetComponent<Image>().color=new Color(.1f,.18f,.2f);var input=go.GetComponent<InputField>();input.shouldHideMobileInput=false;input.lineType=InputField.LineType.SingleLine;input.textComponent=Label(go.transform,"",Vector2.zero,r.sizeDelta,23,Color.white);input.placeholder=Label(go.transform,hint,Vector2.zero,r.sizeDelta,21,new Color(.55f,.65f,.65f));return input;}
        Button Button(Transform p,string text,Vector2 pos,System.Action action){var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=new Vector2(360,70);go.GetComponent<Image>().color=new Color(.05f,.55f,.45f);var b=go.GetComponent<Button>();b.onClick.AddListener(()=>action());Label(go.transform,text,Vector2.zero,r.sizeDelta,20,Color.white);return b;}
        Text Label(Transform p,string value,Vector2 pos,Vector2 size,int sizePx,Color color){var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(p,false);var r=(RectTransform)go.transform;r.anchoredPosition=pos;r.sizeDelta=size;var t=go.GetComponent<Text>();t.font=font;t.text=value;t.fontSize=sizePx;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        void PlacesFound(List<RoutePlannerService.Place> places){if(message==null)return;if(searchingStart){starts.Clear();starts.AddRange(places);message.text="START READY  •  "+places[0].label+"\nNow find your finish address.";}else{ends.Clear();ends.AddRange(places);message.text="FINISH READY  •  "+places[0].label+"\nCalculate your cycling route.";}}
        void RouteFound(RoutePlannerService.RouteResult result){stats.text=$"ROUTE READY\n{result.distance/1000f:F1} km  |  {result.duration/60f:F0} min estimated\nDedicated bicycle routing selected. Press Calculate again after changing addresses.";app.ApplyOnlineRoute(result);}
        void Failed(string error){message.text=error;}
        void Update(){if(app==null||canvas==null)return;if(app.State!=RideState.Menu)Close();else if(!open)canvas.gameObject.SetActive(false);else if(routeRoot.gameObject.activeSelf)RefreshCards();}
        void OnDestroy(){if(planner!=null){planner.PlacesFound-=PlacesFound;planner.RouteFound-=RouteFound;planner.Failed-=Failed;}}
    }
}
