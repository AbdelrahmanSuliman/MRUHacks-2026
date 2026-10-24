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
