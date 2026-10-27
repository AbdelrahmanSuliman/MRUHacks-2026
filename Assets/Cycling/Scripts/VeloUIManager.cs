using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CalgaryCycling
{
    public sealed class VeloUIManager : MonoBehaviour
    {
        RideApp app;
        Canvas canvas;
        Canvas cockpitCanvas;
        RectTransform root;
        Font font;
        Text status, hud, controlsHint, detail, settingsValues;
        RoutePlannerUI addressPlanner;
        GameObject menu, pause, summary, settings;
        GameObject visiblePage;
        Button start, resume, retry;
        bool hudVisible = true;
        float rideSeconds;
        RideState previousState;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        PointerEventData pointer;
        GameObject hovered;
        GameObject gazeCursor;
        Material gazeMaterial;
        Sprite roundedSprite;
        readonly Color background = new Color(.96f, .89f, .70f, .98f);
        readonly Color panel = new Color(.99f, .95f, .82f, .98f);
        readonly Color forest = new Color(.14f, .32f, .20f, .98f);
        readonly Color accent = new Color(.98f, .67f, .18f, 1f);
        readonly Color muted = new Color(.25f, .38f, .25f, 1f);

        public void Initialize(RideApp owner)
        {
            app = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("Velo UI Event System", typeof(EventSystem));
                eventSystemObject.transform.SetParent(transform, false);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null) inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
            pointer = new PointerEventData(eventSystem);
            roundedSprite = MakeRoundedSprite();

            var canvasObject = new GameObject("VELO Dashboard",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(app.Motor.transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = app.Rig.View;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 22;
            scaler.referencePixelsPerUnit = 100;
            root = (RectTransform)canvasObject.transform;
            root.sizeDelta = new Vector2(1120, 760);
            root.localScale = Vector3.one * .0016f;
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .04f, 1.9f);

            addressPlanner = app.GetComponent<RoutePlannerUI>();
            menu = MakePanel("Main Menu");
            pause = MakePanel("Paused");
            summary = MakePanel("Ride Complete");
            settings = MakePanel("Settings");
            BuildMenu();
            BuildPause();
            BuildSummary();
            BuildSettings();
            var cockpit=new GameObject("Bicycle ride display",typeof(RectTransform),typeof(Canvas));
            cockpit.transform.SetParent(app.Motor.transform,false);
            cockpitCanvas=cockpit.GetComponent<Canvas>();cockpitCanvas.renderMode=RenderMode.WorldSpace;
            cockpitCanvas.worldCamera=app.Rig.View;
            var cockpitRect=(RectTransform)cockpit.transform;
            cockpitRect.sizeDelta=new Vector2(720,150);cockpitRect.localScale=Vector3.one*.001f;
            cockpitRect.localPosition=new Vector3(0,1.30f,1.3f);
            var screen=new GameObject("Cockpit glass",typeof(RectTransform),typeof(Image));screen.transform.SetParent(cockpitRect,false);
            ((RectTransform)screen.transform).sizeDelta=cockpitRect.sizeDelta;
            var screenImage=screen.GetComponent<Image>();screenImage.sprite=roundedSprite;screenImage.type=Image.Type.Sliced;
            screenImage.color=new Color(.055f,.14f,.13f,.96f);screenImage.raycastTarget=false;
            hud = Label(cockpitRect, "", new Vector2(0, 28), new Vector2(680, 64), 30, panel);
            controlsHint = Label(cockpitRect, "", new Vector2(0, -38), new Vector2(680, 46), 20, new Color(1f,.83f,.4f));
            gazeCursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gazeCursor.name = "Gaze Cursor";
            Release(gazeCursor.GetComponent<Collider>());
            gazeCursor.transform.SetParent(app.Rig.View.transform, false);
            gazeCursor.transform.localPosition = new Vector3(0, 0, 3f);
            gazeCursor.transform.localScale = Vector3.one * .018f;
            gazeMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            gazeMaterial.color = accent;
            gazeCursor.GetComponent<Renderer>().sharedMaterial = gazeMaterial;
            gazeCursor.SetActive(false);
            Show(menu);
            previousState = app.State;
        }

        GameObject MakePanel(string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root, false);
            ((RectTransform)panel.transform).sizeDelta = root.sizeDelta;
            var image = panel.GetComponent<Image>();
            image.color = background;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            return panel;
        }

        Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject("Text", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        Button MakeButton(Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary = false)
        {
            var item = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = item.GetComponent<Image>();
            image.color = Color.white;
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            var button = item.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            var colors = button.colors;
            colors.normalColor = primary ? accent : forest;
            colors.highlightedColor = new Color(1f, .82f, .32f, 1f);
            colors.pressedColor = new Color(.86f, .52f, .12f, 1f);
            colors.selectedColor = new Color(1f, .82f, .32f, 1f);
            colors.disabledColor = new Color(.65f, .64f, .52f, 1f);
            button.colors = colors;
            Label(item.transform, caption, Vector2.zero, size, 23, primary ? Color.black : Color.white);
            return button;
        }

        void Header(GameObject panel, string heading, string description)
        {
            Label(panel.transform, heading, new Vector2(0, 288), new Vector2(1020, 70), 46, forest);
            Label(panel.transform, description, new Vector2(0, 226), new Vector2(980, 54), 23, muted);
        }

        void BuildMenu()
        {
            Header(menu, "VELO  /  PEACE BRIDGE", "A warm Bow River riverfront loop near Calgary's Peace Bridge.");
            status = Label(menu.transform, "", new Vector2(0, 150), new Vector2(980, 78), 23, forest);
            start = MakeButton(menu.transform, "START RIDE", new Vector2(0, 62), new Vector2(600, 82), () => app.StartRide(), true);
            MakeButton(menu.transform, "PLAN A ROUTE", new Vector2(0, -42), new Vector2(600, 74), () =>
            {
                if (addressPlanner != null) addressPlanner.Show();
            });
            MakeButton(menu.transform, "SETTINGS", new Vector2(-165, -140), new Vector2(290, 66), () => Show(settings));
            retry = MakeButton(menu.transform, "RETRY SCENERY", new Vector2(165, -140), new Vector2(290, 66), () => app.Retry());
            Label(menu.transform, "LOOK at a button, then press A or the right trigger.\nWhile riding: hold both controllers like handlebars   |   X: pause   |   B: menu",
                new Vector2(0, -248), new Vector2(980, 62), 20, muted);
        }

        void BuildPause()
        {
            Header(pause, "RIDE PAUSED", "Take a moment. Head tracking remains active.");
            resume = MakeButton(pause.transform, "RESUME RIDE", new Vector2(0, 92), new Vector2(520, 70), () => app.Resume(), true);
            MakeButton(pause.transform, "RESTART ROUTE", new Vector2(0, 4), new Vector2(520, 64), () => app.Restart());
            MakeButton(pause.transform, "END RIDE", new Vector2(0, -84), new Vector2(520, 64), () =>
            {
                app.Menu();
                Show(menu);
            });
        }

        void BuildSummary()
        {
            Header(summary, "RIDE COMPLETE", "You reached the end of this route.");
            detail = Label(summary.transform, "", new Vector2(0, 84), new Vector2(800, 170), 26, forest);
            MakeButton(summary.transform, "RIDE AGAIN", new Vector2(-140, -82), new Vector2(250, 64), () =>
            {
                app.Restart();
                app.Resume();
            }, true);
            MakeButton(summary.transform, "MAIN MENU", new Vector2(140, -82), new Vector2(250, 64), () =>
            {
                app.Menu();
                Show(menu);
            });
        }

        void BuildSettings()
        {
            Header(settings, "RIDE SETTINGS", "Changes apply to this ride only.");
            MakeButton(settings.transform, "SPEED  −", new Vector2(-150, 96), new Vector2(250, 64),
                () => ChangeSpeed(-.5f));
            MakeButton(settings.transform, "SPEED  +", new Vector2(150, 96), new Vector2(250, 64),
                () => ChangeSpeed(.5f));
            MakeButton(settings.transform, "STEERING  −", new Vector2(-150, 12), new Vector2(250, 64),
                () => ChangeSensitivity(-.1f));
            MakeButton(settings.transform, "STEERING  +", new Vector2(150, 12), new Vector2(250, 64),
                () => ChangeSensitivity(.1f));
            MakeButton(settings.transform, "TOGGLE HUD", new Vector2(0, -72), new Vector2(520, 60), () =>
            {
                hudVisible = !hudVisible;
                hud.gameObject.SetActive(hudVisible);
            });
            MakeButton(settings.transform,"CENTRE HANDLEBARS",new Vector2(0,-150),new Vector2(520,60),()=>app.CalibrateHandlebars());
            settingsValues = Label(settings.transform, "", new Vector2(0, -120), new Vector2(850, 42), 17, muted);
            MakeButton(settings.transform, "BACK", new Vector2(0, -244), new Vector2(320, 58), () =>
                Show(app.State == RideState.Paused ? pause : menu));
        }

        void ChangeSpeed(float amount)
        {
            app.Settings.cruisingSpeed = Mathf.Clamp(app.Settings.cruisingSpeed + amount, 1, 6);
            UpdateDetails();
        }

        void ChangeSensitivity(float amount)
        {
            app.Settings.steeringSensitivity = Mathf.Clamp(app.Settings.steeringSensitivity + amount, .2f, 2);
            UpdateDetails();
        }

        void UpdateDetails()
        {
            if (detail != null)
                detail.text = $"DISTANCE  {app.Motor.Distance / 1000f:F2} km\n" +
                    $"TIME  {rideSeconds / 60f:F0}:{rideSeconds % 60f:00}\n" +
                    $"AVERAGE  {(rideSeconds > 0 ? app.Motor.Distance / rideSeconds * 3.6f : 0):F1} km/h";
            if (settingsValues != null)
                settingsValues.text = $"Cruise: {app.Settings.cruisingSpeed:F1} m/s   |   Steering: {app.Settings.steeringSensitivity:F1}";
            if (status != null)
                status.text = (app.Scenery.Ready?"Ready to ride. Look at START RIDE and press A.":app.Message)+"\n"+
                    app.Scenery.Status+(app.Rig.IsVR && !app.Input.RightControllerDetected?"\nRight controller not detected. Wake it with a button press.":"");
        }

        void Show(GameObject page)
        {
            visiblePage = page;
            menu.SetActive(page == menu);
            pause.SetActive(page == pause);
            summary.SetActive(page == summary);
            settings.SetActive(page == settings);
        }

        void Update()
        {
            if (app == null) return;
            if (hud == null || controlsHint == null) return;
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .04f, 1.9f);

            if (app.State != previousState)
            {
                if (app.State == RideState.Riding && previousState != RideState.Paused)
                    rideSeconds = 0;
                if (app.State == RideState.Riding && app.Motor.Distance == 0)
                    rideSeconds = 0;
                if (app.State == RideState.Menu && previousState != RideState.Menu)
                    Show(menu);
                previousState = app.State;
            }

            bool plannerOpen = addressPlanner != null && addressPlanner.IsOpen;
            bool riding = app.State == RideState.Riding;
            cockpitCanvas.gameObject.SetActive(riding && hudVisible && !plannerOpen);
            gazeCursor.SetActive(app.Rig.IsVR && !riding);
            canvas.gameObject.SetActive(!plannerOpen);
            if (plannerOpen)
            {
                if (app.Input.Menu) addressPlanner.Hide();
                if (app.Rig.IsVR) HandleGazeSelection(addressPlanner.InteractionCanvas);
                else return;
                return;
            }

            if (riding) rideSeconds += Time.deltaTime;
            hud.text = $"{app.Motor.Speed * 3.6f:F1} km/h    |    " +
                $"{app.Motor.Distance/1000f:F2} / {app.Motor.Route.Length/1000f:F2} km    |    " +
                $"{app.Motor.Progress:P0}";
            controlsHint.text=app.Rig.IsVR
                ? (app.Input.HandlebarActive?"HANDLEBARS CONNECTED   |   X: pause":"Move both controllers like handlebars   |   X: pause")
                : "A / D: steer left and right   |   SPACE: pause";
            hud.gameObject.SetActive(riding && hudVisible);
            controlsHint.gameObject.SetActive(riding && hudVisible);

            if (riding)
                Show(null);
            else
            {
                if (app.State == RideState.Paused) Show(pause);
                else if (app.State == RideState.Finished)
                {
                    UpdateDetails();
                    Show(summary);
                }
                else if (app.State == RideState.Menu || app.State == RideState.Error)
                {
                    UpdateDetails();
                    if (visiblePage == null) Show(menu);
                }

                start.interactable = app.CanRide && app.State==RideState.Menu;
                resume.interactable = app.CanRide && app.State==RideState.Paused;
                retry.gameObject.SetActive(app.Scenery.Failed);
            }

            if (app.Rig.IsVR && !riding) HandleGazeSelection(canvas.transform);
        }

        void HandleGazeSelection(Transform interactionCanvas)
        {
            if (interactionCanvas == null) return;
            var camera = app.Rig.View;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            var canvasPlane = new Plane(interactionCanvas.forward, interactionCanvas.position);
            if (!canvasPlane.Raycast(ray, out var distance)) return;
            var gazePoint = ray.GetPoint(distance);
            pointer.position = camera.WorldToScreenPoint(gazePoint);
            gazeCursor.transform.position = gazePoint;
            gazeCursor.transform.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject target = null;
            foreach (var hit in hits)
            {
                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable())
                {
                    target = selectable.gameObject;
                    break;
                }
            }

            if (target != hovered)
            {
                if (hovered != null)
                    ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
                hovered = target;
                if (hovered != null)
                    ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerEnterHandler);
            }
            if (hovered != null && app.Input.Select)
            {
                ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerClickHandler);
            }
        }

        void OnDestroy()
        {
            if (gazeMaterial != null) Release(gazeMaterial);
            if (roundedSprite != null)
            {
                var texture = roundedSprite.texture;
                Release(roundedSprite);
                if (texture != null) Release(texture);
            }
        }

        static void Release(UnityEngine.Object resource)
        {
            if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);
        }

        Sprite MakeRoundedSprite()
        {
            const int size = 64;
            const int radius = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Velo Rounded Panel" };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0, x - (size - radius - 1));
                    float dy = Mathf.Max(radius - y, 0, y - (size - radius - 1));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, dx * dx + dy * dy <= radius * radius ? 1f : 0f);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
