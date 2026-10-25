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
            // Desktop already has Cesium's screen footer. Quest needs a texture
            // on a world-space canvas so the attribution is visible in both eyes.
            if(Application.platform!=RuntimePlatform.Android && !UnityEngine.XR.XRSettings.isDeviceActive)return;
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
            rect.sizeDelta=new Vector2(1600,300);rect.localScale=Vector3.one*.0007f;
            rect.localPosition=new Vector3(0,-.84f,2f);
            var bg=new GameObject("Background",typeof(RectTransform),typeof(UnityEngine.UI.Image));bg.transform.SetParent(rect,false);
            var br=(RectTransform)bg.transform;br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.sizeDelta=Vector2.zero;
            bg.GetComponent<UnityEngine.UI.Image>().color=new Color(.015f,.025f,.04f,.95f);
            var img=new GameObject("Live provider credits",typeof(RectTransform),typeof(RawImage));img.transform.SetParent(rect,false);
            var ir=(RectTransform)img.transform;ir.sizeDelta=new Vector2(1600,260);ir.anchoredPosition=new Vector2(0,-20);
            img.GetComponent<RawImage>().texture=texture;img.GetComponent<RawImage>().raycastTarget=false;
            var label=new GameObject("Map source",typeof(RectTransform),typeof(Text));label.transform.SetParent(rect,false);
            var lr=(RectTransform)label.transform;lr.sizeDelta=new Vector2(1560,34);lr.anchoredPosition=new Vector2(0,130);
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=24;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;
            text.text="Scenery: Cesium ion terrain, aerial imagery and OSM Buildings  |  Bicycle and demo route: Calgary Cycling Simulator";
        }
        void OnDestroy()
        {
            if(document!=null)document.panelSettings=previous;
            if(texture!=null) { texture.Release();Destroy(texture); }
            if(panel!=null)Destroy(panel);
        }
    }
}
