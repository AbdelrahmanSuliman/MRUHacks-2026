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
        readonly Color background = new Color(.018f, .035f, .045f, .96f);
        readonly Color accent = new Color(.12f, .82f, .78f);
        readonly Color muted = new Color(.62f, .72f, .75f);

        public void Initialize(RideApp owner)
        {
            app = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystem = new GameObject("Velo UI Event System",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            pointer = new PointerEventData(eventSystem.GetComponent<EventSystem>());

            var canvasObject = new GameObject("VELO Dashboard",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(app.Motor.transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = app.Rig.View;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 22;
            scaler.referencePixelsPerUnit = 100;
            root = (RectTransform)canvasObject.transform;
            root.sizeDelta = new Vector2(1000, 680);
            root.localScale = Vector3.one * .0018f;
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .04f, 3.2f);

            addressPlanner = app.GetComponent<RoutePlannerUI>();
            menu = MakePanel("Main Menu");
            pause = MakePanel("Paused");
            summary = MakePanel("Ride Complete");
            settings = MakePanel("Settings");
            BuildMenu();
            BuildPause();
            BuildSummary();
            BuildSettings();
            hud = Label(root, "", new Vector2(0, 300), new Vector2(960, 64), 24, Color.white);
            controlsHint = Label(root, "A / D steer   •   SPACE pause   •   Quest: left stick steer, X pause",
                new Vector2(0, 248), new Vector2(940, 38), 16, muted);
            gazeCursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gazeCursor.name = "Gaze Cursor";
            Destroy(gazeCursor.GetComponent<Collider>());
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
            panel.GetComponent<Image>().color = background;
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
            image.color = primary ? accent : new Color(.08f, .16f, .18f, .98f);
            var button = item.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            var colors = button.colors;
            colors.highlightedColor = new Color(.3f, .95f, .9f);
            colors.disabledColor = new Color(.25f, .3f, .31f);
            button.colors = colors;
            Label(item.transform, caption, Vector2.zero, size, 23, primary ? Color.black : Color.white);
            return button;
        }

        void Header(GameObject panel, string heading, string description)
        {
            Label(panel.transform, heading, new Vector2(0, 260), new Vector2(900, 64), 38, accent);
            Label(panel.transform, description, new Vector2(0, 205), new Vector2(900, 48), 19, muted);
        }

        void BuildMenu()
        {
            Header(menu, "VELO  /  CALGARY", "A continuous, guided ride through the city.");
            status = Label(menu.transform, "", new Vector2(0, 142), new Vector2(860, 68), 19, Color.white);
            start = MakeButton(menu.transform, "START RIDE", new Vector2(0, 52), new Vector2(520, 70), () => app.StartRide(), true);
            MakeButton(menu.transform, "PLAN A ROUTE", new Vector2(0, -36), new Vector2(520, 64), () =>
            {
                if (addressPlanner != null) addressPlanner.Show();
            });
            MakeButton(menu.transform, "SETTINGS", new Vector2(-140, -124), new Vector2(250, 58), () => Show(settings));
            retry = MakeButton(menu.transform, "RETRY SCENERY", new Vector2(140, -124), new Vector2(250, 58), () => app.Retry());
            Label(menu.transform, "Choose a destination for a continuous cycling route, or try the short demo.\nDesktop: A / D steer, SPACE pause   •   Quest: left stick steer, X pause",
                new Vector2(0, -226), new Vector2(880, 48), 17, muted);
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
            detail = Label(summary.transform, "", new Vector2(0, 84), new Vector2(800, 170), 26, Color.white);
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
            settingsValues = Label(settings.transform, "", new Vector2(0, -120), new Vector2(850, 42), 17, muted);
            MakeButton(settings.transform, "BACK", new Vector2(0, -206), new Vector2(320, 58), () =>
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
                status.text = app.Message + "\n" + app.Scenery.Status;
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
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .04f, 3.2f);

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
            gazeCursor.SetActive(app.Rig.IsVR && !riding);
            canvas.gameObject.SetActive(!plannerOpen);
            if (plannerOpen)
            {
                if (app.Input.Menu) addressPlanner.Hide();
                else return;
            }

            if (riding) rideSeconds += Time.deltaTime;
            hud.text = $"{app.Motor.Speed * 3.6f:F1} km/h     " +
                $"{app.Motor.Distance:F0} / {app.Motor.Route.Length:F0} m     " +
                $"{app.Motor.Progress:P0}";
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

                start.interactable = app.Scenery.Ready || app.Scenery.Failed;
                resume.interactable = app.Scenery.Ready || app.Scenery.Failed;
                retry.gameObject.SetActive(app.Scenery.Failed);
            }

            if (app.Rig.IsVR && !riding) HandleGazeSelection();
        }

        void HandleGazeSelection()
        {
            var camera = app.Rig.View;
            pointer.position = camera.WorldToScreenPoint(camera.transform.position + camera.transform.forward * 3f);
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
            if (gazeMaterial != null) Destroy(gazeMaterial);
        }
    }
}
