using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CalgaryCycling
{
    // Diegetic route-planning screen. The route itself remains owned by RideApp;
    // this class only handles address selection, confirmation, and launch.
    public sealed class RoutePlannerUI : MonoBehaviour
    {
        readonly List<RoutePlannerService.Place> starts = new List<RoutePlannerService.Place>();
        readonly List<RoutePlannerService.Place> ends = new List<RoutePlannerService.Place>();
        readonly List<GameObject> suggestionObjects = new List<GameObject>();

        RideApp app;
        RoutePlannerService planner;
        Canvas canvas;
        RectTransform root;
        Font font;
        InputField start;
        InputField end;
        Text message;
        Text startSelection;
        Text endSelection;
        Text routeSummary;
        Text suggestionHeading;
        Button calculate;
        Button ride;
        Button searchStart;
        Button searchEnd;
        bool searchingStart = true;
        bool open;
        bool routeReady;
        bool busy;
        RoutePlannerService.Place selectedStart;
        RoutePlannerService.Place selectedEnd;
        Sprite roundedSprite;

        readonly Color cream = new Color(.99f, .95f, .82f, .99f);
        readonly Color pale = new Color(.96f, .89f, .70f, 1f);
        readonly Color forest = new Color(.14f, .32f, .20f, 1f);
        readonly Color muted = new Color(.25f, .38f, .25f, 1f);
        readonly Color accent = new Color(.98f, .67f, .18f, 1f);
        readonly Color error = new Color(.65f, .17f, .13f, 1f);

        public bool IsOpen { get { return open; } }
        public Transform InteractionCanvas { get { return canvas == null ? null : canvas.transform; } }

        public void Show()
        {
            open = true;
            if (canvas != null) canvas.gameObject.SetActive(true);
            RefreshState();
        }

        public void Hide()
        {
            open = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        public void Initialize(RideApp owner, RoutePlannerService service)
        {
            app = owner;
            planner = service;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            roundedSprite = MakeRoundedSprite();

            planner.PlacesFound += PlacesFound;
            planner.RouteFound += RouteFound;
            planner.Failed += Failed;

            var go = new GameObject("Calgary Route Planner", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(app.Motor.transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = app.Rig.View;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;
            root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(1120, 900);
            root.localScale = Vector3.one * .0016f;
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .05f, 1.85f);

            var card = new GameObject("Route Planner Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(root, false);
            var cardImage = card.GetComponent<Image>();
            cardImage.sprite = roundedSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = cream;
            ((RectTransform)card.transform).sizeDelta = root.sizeDelta;

            Label(root, "PLAN YOUR CALGARY RIDE", new Vector2(0, 426), new Vector2(1100, 66), 46, forest);
            Label(root, "Choose real addresses, preview the cycling route, then ride it.", new Vector2(0, 374), new Vector2(1080, 42), 23, muted);

            Label(root, "START ADDRESS", new Vector2(-100, 350), new Vector2(520, 28), 17, forest);
            start = Field(root, "Search a Calgary place", new Vector2(-100, 298));
            start.text = "Peace Bridge, Calgary, Alberta";
            searchStart = Button(root, "FIND START", new Vector2(320, 298), new Vector2(220, 70), SearchStart, false);
            startSelection = Label(root, "No start selected yet", new Vector2(-120, 244), new Vector2(850, 34), 18, muted);

            Label(root, "FINISH ADDRESS", new Vector2(-100, 228), new Vector2(520, 28), 17, forest);
            end = Field(root, "Search a Calgary place", new Vector2(-100, 176));
            end.text = "1300 Zoo Rd NE, Calgary, Alberta";
            searchEnd = Button(root, "FIND FINISH", new Vector2(320, 176), new Vector2(220, 70), SearchEnd, false);
            endSelection = Label(root, "No finish selected yet", new Vector2(-120, 122), new Vector2(850, 34), 18, muted);
            start.onValueChanged.AddListener(_=>AddressEdited(true));
            end.onValueChanged.AddListener(_=>AddressEdited(false));

            suggestionHeading = Label(root, "SEARCH RESULTS", new Vector2(0, 66), new Vector2(1020, 40), 21, forest);
            message = Label(root, "Search for a start or finish address to see Calgary matches.", new Vector2(0, 20), new Vector2(1050, 38), 19, muted);

            calculate = Button(root, "BUILD CYCLING ROUTE", new Vector2(-150, -344), new Vector2(280, 76), CalculateRoute, true);
            ride = Button(root, "RIDE THIS ROUTE", new Vector2(150, -344), new Vector2(280, 76), RideSelectedRoute, true);
            routeSummary = Label(root, "", new Vector2(0, -254), new Vector2(1040, 78), 23, forest);
            Button(root, "BACK TO DASHBOARD", new Vector2(0, -432), new Vector2(280, 58), Hide, false);

            canvas.gameObject.SetActive(false);
            RefreshState();
        }

        InputField Field(Transform parent, string hint, Vector2 position)
        {
            var go = new GameObject("Address Field", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(520, 76);
            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = pale;
            var input = go.GetComponent<InputField>();
            input.shouldHideMobileInput = false;
            input.lineType = InputField.LineType.SingleLine;
            var valueText = Label(go.transform, "", Vector2.zero, rect.sizeDelta - new Vector2(34, 0), 25, forest);
            var placeholderText = Label(go.transform, hint, Vector2.zero, rect.sizeDelta - new Vector2(34, 0), 23, muted);
            valueText.alignment = TextAnchor.MiddleLeft;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            input.textComponent = valueText;
            input.placeholder = placeholderText;
            return input;
        }

        Button Button(Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary)
        {
            var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            var colors = button.colors;
            colors.normalColor = primary ? accent : forest;
            colors.highlightedColor = new Color(1f, .82f, .32f, 1f);
            colors.pressedColor = new Color(.86f, .52f, .12f, 1f);
            colors.selectedColor = new Color(1f, .82f, .32f, 1f);
            colors.disabledColor = new Color(.70f, .67f, .53f, 1f);
            button.colors = colors;
            Label(go.transform, caption, Vector2.zero, size - new Vector2(20, 0), primary ? 22 : 20, primary ? Color.black : Color.white);
            return button;
        }

        Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
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

        void SearchStart()
        {
            if(busy)return;
            searchingStart = true;
            selectedStart = null;
            starts.Clear();
            routeReady = false;
            ShowSearching("Finding Calgary start places...");
            planner.SearchCalgary(start == null ? "" : start.text);
        }

        void SearchEnd()
        {
            if(busy)return;
            searchingStart = false;
            selectedEnd = null;
            ends.Clear();
            routeReady = false;
            ShowSearching("Finding Calgary finish places...");
            planner.SearchCalgary(end == null ? "" : end.text);
        }

        void ShowSearching(string text)
        {
            busy=true;
            if (message != null)
            {
                message.color = muted;
                message.text = text;
            }
            ClearSuggestions();
            RefreshState();
        }

        void PlacesFound(List<RoutePlannerService.Place> places)
        {
            busy=false;
            if (places == null || places.Count == 0)
            {
                Failed("No matching Calgary places were found.");
                return;
            }

            if (searchingStart)
            {
                starts.Clear();
                starts.AddRange(places);
                message.text = "Choose the start address that matches your ride.";
            }
            else
            {
                ends.Clear();
                ends.AddRange(places);
                message.text = "Choose the finish address that matches your ride.";
            }
            message.color = forest;
            suggestionHeading.text = searchingStart ? "START MATCHES" : "FINISH MATCHES";
            RenderSuggestions(places);
            RefreshState();
        }

        void RenderSuggestions(List<RoutePlannerService.Place> places)
        {
            ClearSuggestions();
            int count = Mathf.Min(3, places.Count);
            for (int i = 0; i < count; i++)
            {
                var captured = places[i];
                float y = -42 - i * 62;
                var button = Button(root, (i + 1) + "  " + ShortLabel(captured.label), new Vector2(0, y), new Vector2(900, 54),
                    () => SelectPlace(captured), false);
                suggestionObjects.Add(button.gameObject);
            }
        }

        string ShortLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Calgary location";
            return value.Length > 76 ? value.Substring(0, 73) + "..." : value;
        }

        void SelectPlace(RoutePlannerService.Place place)
        {
            if (place == null) return;
            if (searchingStart)
            {
                selectedStart = place;
                if (start != null) start.SetTextWithoutNotify(place.label);
                startSelection.text = "START SELECTED  •  " + ShortLabel(place.label);
                startSelection.color = forest;
                message.text = "Start selected. Now search for and select your finish.";
            }
            else
            {
                selectedEnd = place;
                if (end != null) end.SetTextWithoutNotify(place.label);
                endSelection.text = "FINISH SELECTED  •  " + ShortLabel(place.label);
                endSelection.color = forest;
                message.text = "Finish selected. Build the real cycling route when ready.";
            }
            message.color = forest;
            ClearSuggestions();
            suggestionHeading.text = "SEARCH RESULTS";
            RefreshState();
        }

        void CalculateRoute()
        {
            if(busy)return;
            if (selectedStart == null || selectedEnd == null)
            {
                message.color = error;
                message.text = "Select one start and one finish address first.";
                return;
            }
            routeReady = false;
            message.color = muted;
            message.text = "Building a real Calgary cycling route...";
            routeSummary.text = "";
            busy=true;
            planner.BicycleRoute(selectedStart, selectedEnd);
            RefreshState();
        }

        void RouteFound(RoutePlannerService.RouteResult result)
        {
            busy=false;
            if (result == null || result.points == null || result.points.Count < 2)
            {
                Failed("The service returned no usable route geometry.");
                return;
            }

            if (!app.ApplyOnlineRoute(result))
            {
                routeReady = false;
                message.color = error;
                message.text = "The route could not be placed in Calgary. Try two nearby addresses.";
                return;
            }

            routeReady = true;
            message.color = forest;
            message.text = "Your Calgary cycling route is ready. Select Ride This Route to begin.";
            routeSummary.text = "REAL ROUTE READY\n" +
                (result.distance / 1000f).ToString("F1") + " km  •  " +
                (result.duration / 60f).ToString("F0") + " min estimated  •  cycling streets and paths";
            RefreshState();
        }

        void RideSelectedRoute()
        {
            if (!routeReady) return;
            app.StartRide();
            if (app.State == RideState.Riding) Hide();
            else
            {
                message.color = muted;
                message.text = "The route is ready. Wait for Calgary scenery to finish loading.";
            }
        }

        void Failed(string text)
        {
            busy=false;
            if (message == null) return;
            message.color = error;
            message.text = string.IsNullOrWhiteSpace(text) ? "Request failed. Check the address and try again." : text;
            routeReady = false;
            RefreshState();
        }

        void RefreshState()
        {
            if (calculate != null)
                calculate.interactable = !busy && selectedStart != null && selectedEnd != null && !routeReady;
            if (ride != null)
                ride.interactable = routeReady && app != null && app.State == RideState.Menu &&
                    app.Scenery != null && (app.Scenery.Ready || app.Scenery.Failed);
            if (searchStart != null) searchStart.interactable = planner != null && !busy;
            if (searchEnd != null) searchEnd.interactable = planner != null && !busy;
            if(start!=null)start.interactable=!busy;
            if(end!=null)end.interactable=!busy;
        }

        void AddressEdited(bool isStart)
        {
            routeReady=false;
            if(routeSummary!=null)routeSummary.text="";
            if(isStart){selectedStart=null;startSelection.text="Search and select a start address";}
            else{selectedEnd=null;endSelection.text="Search and select a finish address";}
            ClearSuggestions();
            RefreshState();
        }

        void ClearSuggestions()
        {
            for (int i = 0; i < suggestionObjects.Count; i++)
                if (suggestionObjects[i] != null){suggestionObjects[i].SetActive(false);Destroy(suggestionObjects[i]);}
            suggestionObjects.Clear();
        }

        void Update()
        {
            if (app == null || canvas == null) return;
            root.localPosition = new Vector3(0, app.Settings.eyeHeight + .05f, 1.85f);
            canvas.gameObject.SetActive(open);
            if (open) RefreshState();
        }

        void OnDestroy()
        {
            if (planner != null)
            {
                planner.PlacesFound -= PlacesFound;
                planner.RouteFound -= RouteFound;
                planner.Failed -= Failed;
            }
            ClearSuggestions();
            if (roundedSprite != null)
            {
                var texture = roundedSprite.texture;
                Destroy(roundedSprite);
                if (texture != null) Destroy(texture);
            }
        }

        Sprite MakeRoundedSprite()
        {
            const int size = 64;
            const int radius = 12;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Calgary Route Planner Rounded UI" };
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
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
