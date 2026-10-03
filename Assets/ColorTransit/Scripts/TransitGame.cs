using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColorTransit
{
    public sealed partial class TransitGame : MonoBehaviour
    {
        static Color Background = Hex("101C2E");
        static readonly Color Panel = Hex("1C2D44"), Muted = Hex("8DA4C1"), Cream = Hex("F3F6F4");
        static readonly Color[] Palette = { Hex("FF776C"), Hex("56C7FF"), Hex("65DFB0"), Hex("FFD36A"), Hex("B59AFF"), Hex("FF9BD3") };
        static readonly string[] Symbols = { "R", "B", "G", "Y", "V", "P" };
        TransitModel model;
        readonly Stack<TransitModel> history = new Stack<TransitModel>();
        RectTransform root;
        Font font;
        Sprite rounded;
        bool busy;
        bool smokeMode;
        int hintId = -1;
        float yardScroll = 1;
        string message = "Choose a bus for the first passenger.";
        Sprite busArt, iconArt;

        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            smokeMode = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-transit-smoke") >= 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent")) smokeMode = intent.Call<bool>("getBooleanExtra", "transit_qa", false);
#endif
#endif
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeRounded();
            var busTexture = Resources.Load<Texture2D>("Art/Bus");
            if (busTexture != null) busArt = Sprite.Create(busTexture, new Rect(0,0,busTexture.width,busTexture.height), new Vector2(.5f,.5f));
            var iconTexture = Resources.Load<Texture2D>("Art/TransitIcon");
            if (iconTexture != null) iconArt = Sprite.Create(iconTexture, new Rect(0,0,iconTexture.width,iconTexture.height), new Vector2(.5f,.5f));
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("Input", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("Transit UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960); scaler.matchWidthOrHeight = .5f;
            root = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>(); root.SetParent(canvas.transform, false);
            FitSafeArea();
            RestoreProgress(); Draw();
            if (smokeMode) StartCoroutine(Smoke());
        }
        void FitSafeArea()
        {
            var safe = Screen.safeArea;
            Canvas.ForceUpdateCanvases();
            float scale = root.GetComponentInParent<Canvas>().scaleFactor;
            root.anchorMin = root.anchorMax = new Vector2(safe.center.x / Screen.width, safe.center.y / Screen.height);
            root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(540, 960);
            root.localScale = Vector3.one * Mathf.Min(safe.width / scale / 540, safe.height / scale / 960);
        }
        void Update()
        {
            if (adsDirty && !busy && root != null) { adsDirty = false; Draw(); }
            if (root != null && (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea)) { FitSafeArea(); lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == "home") Application.Quit();
                else if (ads == null || !ads.Showing) Go(screen == "game" ? "pause" : "home");
            }
        }
        int lastWidth, lastHeight;
        Rect lastSafe;
        void Load(int level)
        {
            busy = false; model = TransitModel.Generate(level); history.Clear(); hintId = -1; yardScroll = 1;
            message = level == 1 ? "Start with the color at the front of the queue." : "Plan your dispatch. Keep a bay available.";
            awardedNow = 0; Persist(); Draw();
        }
        IEnumerator Smoke()
        {
            screen = "game"; model = TransitModel.Generate(1); history.Clear(); Draw();
            yield return new WaitForSeconds(.5f);
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "QA");
            System.IO.Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "01-gameplay.png"));
            yield return new WaitForSeconds(.3f);
            while (!model.Won)
            {
                int id = model.Hint();
                if (id < 0) { Debug.LogError("SMOKE FAILED: no valid dispatch"); Application.Quit(1); yield break; }
                Dispatch(id);
                while (busy) yield return null;
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "02-complete.png"));
            yield return new WaitForSeconds(.3f);
            Undo();
            if (model.Won || model.Cursor == model.Queue.Count) Debug.LogError("SMOKE FAILED: undo");
            model = TransitModel.Generate(9); history.Clear(); Draw();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "03-later-route.png"));
            yield return new WaitForSeconds(.3f);
            Debug.Log("TRANSIT SMOKE PASS: animated boarding, route completion, undo, and later route render.");
            Application.Quit();
        }
        void Draw()
        {
            for (int i = root.childCount - 1; i >= 0; i--) { var child = root.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            Box(root, "Background", 0, 0, 540, 960, Background, false);
            if (DrawScreen()) return;
            Label(root, "TRANSIT / CITY PUZZLES", 28, 23, 370, 20, 12, Muted, TextAnchor.MiddleLeft);
            Label(root, "Color Transit", 28, 47, 400, 43, 34, Cream, TextAnchor.MiddleLeft, true);
            Button(root, "PAUSE", 420, 36, 92, 44, Panel, () => Go("pause"), 12);
            Box(root, "Level", 28, 108, 484, 69, Panel);
            Label(root, "ROUTE " + model.Level.ToString("00"), 45, 119, 170, 24, 19, Cream, TextAnchor.MiddleLeft, true);
            Label(root, model.Level < 4 ? "FIRST DEPARTURES" : model.Level < 7 ? "CITY CONNECTIONS" : "PARKING LANES", 45, 146, 225, 15, 10, Muted, TextAnchor.MiddleLeft);
            Label(root, model.Cursor + " / " + model.Queue.Count + " boarded", 301, 124, 190, 26, 15, Cream, TextAnchor.MiddleRight);
            Box(root, "Progress track", 28, 189, 484, 5, Panel);
            if (model.Cursor > 0) Box(root, "Progress", 28, 189, 484f * model.Cursor / model.Queue.Count, 5, Palette[2]);
            Label(root, "PASSENGER QUEUE", 28, 216, 250, 20, 12, Muted, TextAnchor.MiddleLeft, true);
            Label(root, "BOARDS LEFT FIRST", 365, 216, 147, 20, 10, Palette[2], TextAnchor.MiddleRight);
            Box(root, "Queue platform", 28, 247, 484, 100, Panel);
            int shown = Mathf.Min(8, model.Queue.Count - model.Cursor);
            for (int i = 0; i < shown; i++)
            {
                // The front passenger is leftmost, visibly marked below.
                float x = 46 + i * 57;
                Passenger(root, model.Queue[model.Cursor + i], x, 261);
                if (i == 0) Label(root, "NEXT", x - 3, 320, 44, 16, 10, Cream);
            }
            if (shown == 0) Label(root, "Everyone is on their way!", 48, 270, 444, 44, 19, Palette[2]);
            Label(root, "BOARDING BAYS", 28, 373, 250, 20, 12, Muted, TextAnchor.MiddleLeft, true);
            Label(root, "3 spaces · choose wisely", 278, 373, 234, 20, 11, Muted, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                float x = 28 + i * 166;
                Box(root, "Bay " + i, x, 407, 152, 145, Panel);
                if (model.Bays[i] == null)
                {
                    Label(root, "+", x + 51, 432, 50, 50, 35, Hex("3C5672"));
                    Label(root, "BAY " + (i + 1), x, 505, 152, 23, 11, Muted);
                }
                else Bus(root, model.Bays[i], x + 14, 424, 124, 104, false);
            }
            Box(root, "Road", 0, 570, 540, 29, Hex("15253A"), false);
            for (int i = 0; i < 10; i++) Box(root, "Lane marking", i * 60 + 9, 583, 29, 3, Hex("3D5169"), false);
            Label(root, "DISPATCH YARD", 28, 619, 260, 20, 12, Muted, TextAnchor.MiddleLeft, true);
            Label(root, model.Lot.Count + " waiting", 362, 619, 150, 20, 12, Muted, TextAnchor.MiddleRight);
            // Scrollable yard keeps later levels usable on a phone.
            var viewport = Box(root, "Yard viewport", 23, 650, 494, 188, Background, false);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Vehicles", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, 1);
            content.sizeDelta = new Vector2(494, Mathf.Max(188, Mathf.Ceil(model.Lot.Count / 4f) * 94));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < model.Lot.Count; i++) Bus(content, model.Lot[i], 5 + (i % 4) * 124, (i / 4) * 94, 113, 85, true);
            scroll.verticalNormalizedPosition = yardScroll;
            scroll.onValueChanged.AddListener(position => yardScroll = position.y);
            Label(root, message, 28, 844, 484, 32, 13, model.Blocked ? Palette[0] : Cream);
            Button(root, "UNDO" + (history.Count > 0 ? "  " + history.Count : ""), 28, 892, 148, 44, Panel, Undo, 13, !busy && history.Count > 0);
            Button(root, "HINT", 196, 892, 148, 44, Panel, Hint, 13, !busy && !model.Won && !model.Blocked);
            Button(root, "RETRY", 364, 892, 148, 44, Panel, () => Load(model.Level), 13, !busy);
            if (model.Won && !busy) ShowModal(true);
            else if (model.Blocked && !busy) ShowModal(false);
        }
        void Dispatch(int id)
        {
            if (busy || screen != "game" || ads.Showing) return;
            var snapshot = model.Copy();
            if (!model.Dispatch(id)) { message = "Clear the front bus in this lane first."; audioSystem.Play("Jam"); Draw(); return; }
            history.Push(snapshot); hintId = -1; busy = true;
            Persist(); audioSystem.Play("Tap");
            if (save.haptics && Application.platform == RuntimePlatform.Android) Handheld.Vibrate();
            arrivingId = id; message = "Arriving at the station..."; Draw(); StartCoroutine(Board());
        }
        int arrivingId = -1;
        IEnumerator Board()
        {
            if (arrivingId >= 0)
            {
                var bus = root.Find("Bus " + arrivingId) as RectTransform;
                if (bus != null)
                {
                    Vector2 end = bus.anchoredPosition, start = new Vector2(end.x, -620);
                    for (float t = 0; t < 1; t += Time.deltaTime / .3f) { bus.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0,1,t)); yield return null; }
                    bus.anchoredPosition = end;
                }
                arrivingId = -1;
            }
            yield return new WaitForSeconds(.18f);
            while (model.CanBoard)
            {
                int bay = System.Array.FindIndex(model.Bays, v => v != null && v.Color == model.Queue[model.Cursor]);
                var rider = Box(root, "Boarding passenger", 46, 261, 43, 54, Color.clear, false);
                Passenger(rider, model.Queue[model.Cursor], 0, 0);
                Vector2 start = rider.anchoredPosition, end = new Vector2(82 + bay * 166, -440);
                for (float t = 0; t < 1; t += Time.deltaTime / .18f)
                {
                    rider.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                    yield return null;
                }
                var boardingBus = model.Bays[bay];
                if (boardingBus.Boarded + 1 == boardingBus.Capacity)
                {
                    var visual = root.Find("Bus " + boardingBus.Id) as RectTransform;
                    if (visual != null)
                    {
                        audioSystem.Play("Depart"); var from = visual.anchoredPosition;
                        for (float t = 0; t < 1; t += Time.deltaTime / .24f) { visual.anchoredPosition = Vector2.Lerp(from, new Vector2(570,from.y),t*t); yield return null; }
                    }
                }
                model.BoardOne(); Persist(); audioSystem.Play("Board");
                message = "Matching passengers are boarding."; Draw();
                yield return new WaitForSeconds(.13f);
            }
            busy = false;
            message = model.Blocked ? "Station full. Undo and try a different order." : "Choose a bus for the first passenger.";
            if (model.Won) FinishRoute();
            if (model.Blocked) audioSystem.Play("Jam");
            if (pauseRequested) { pauseRequested = false; screen = "pause"; }
            Draw();
        }
        void Undo()
        {
            if (busy || history.Count == 0) return;
            model = history.Pop(); hintId = -1; message = "Move undone. Try a different dispatch."; audioSystem.Play("Undo"); Persist(); Draw();
        }
        void Hint()
        {
            hintId = model.Hint();
            int index = model.Lot.FindIndex(v => v.Id == hintId);
            float overflow = Mathf.Ceil(model.Lot.Count / 4f) * 94 - 188;
            if (index >= 0 && overflow > 0) yardScroll = 1 - Mathf.Clamp01((index / 4) * 94f / overflow);
            message = hintId >= 0 ? "Dispatch the highlighted bus to keep a clear route." : "Undo or retry to find a clear route."; Draw();
        }
        void ShowModal(bool won)
        {
            Box(root, "Dim", 0, 0, 540, 960, new Color(0, .02f, .06f, .86f), false);
            Box(root, "Result", 44, 310, 452, 326, Panel);
            Label(root, won ? "ROUTE COMPLETE" : "STATION FULL", 64, 341, 412, 28, 13, won ? Palette[2] : Palette[0]);
            Label(root, won ? "Smooth travels." : "A little traffic jam.", 64, 384, 412, 45, 29, Cream, TextAnchor.MiddleCenter, true);
            Label(root, won ? model.Queue.Count + " passengers safely home.\n" + (awardedNow > 0 ? "+25 station coins" : "Route replay complete") : "Every bay is occupied, but the next passenger\nneeds a different color. You can undo for free.", 73, 443, 394, 65, 15, Muted);
            Button(root, won ? "NEXT ROUTE  >" : "UNDO LAST DISPATCH", 77, 532, 386, 52, won ? Palette[2] : Palette[0], won ? (UnityEngine.Events.UnityAction)NextRoute : Undo, 15);
            if (!won) Button(root, "RESTART ROUTE", 142, 590, 256, 30, Panel, () => Load(model.Level), 11);
            else if (ads.OffersAds) Button(root, ads.Ready ? "WATCH AD / +25 COINS" : ads.Participating ? "AD NOT READY" : "AD OPTIONS / OPTIONAL +25", 110, 589, 320, 34, Panel, () => { if (!ads.Participating) Go("adprivacy"); else ads.Reward(() => { save.coins = Mathf.Min(10000000,save.coins+25); Persist(); }, Draw); }, 11, ads.Ready || !ads.Participating);
        }
        void Passenger(Transform parent, int color, float x, float y)
        {
            Box(parent, "Passenger head", x + 10, y, 23, 23, Palette[color]);
            Box(parent, "Passenger body", x + 3, y + 25, 37, 29, Palette[color]);
            Label(parent, Symbols[color], x + 4, y + 29, 35, 21, 12, Background, TextAnchor.MiddleCenter, true);
        }
        void Bus(Transform parent, Vehicle v, float x, float y, float w, float h, bool selectable)
        {
            if (v.Id == hintId && selectable) Box(parent, "Hint outline", x - 3, y - 3, w + 6, h + 6, Cream);
            var body = Box(parent, "Bus " + v.Id, x, y, w, h, Palette[v.Color]);
            if (busArt != null) { var im = body.GetComponent<Image>(); im.sprite = busArt; im.type = Image.Type.Simple; }
            Label(body, Symbols[v.Color] + "  /  " + (selectable ? v.Capacity + " seats" : v.Boarded + " / " + v.Capacity), w*.18f, h*.22f, w*.64f, h*.23f, 12, Cream, TextAnchor.MiddleCenter, true);
            for (int s = 0; s < v.Capacity; s++) Box(body, "Seat", w*.3f + s * (w*.4f / v.Capacity), h*.65f, w*.34f / v.Capacity - 1, 6, s < v.Boarded ? Cream : new Color(0, 0, 0, .3f));
            if (selectable)
            {
                bool accessible = model.IsAccessible(v);
                if (model.Level >= 7) Label(body, accessible ? "LANE " + (v.Id % 3 + 1) : "WAIT " + (v.Id % 3 + 1), w*.23f, h*.46f, w*.54f, h*.16f, 8, Background);
                var button = body.gameObject.AddComponent<Button>(); button.targetGraphic = body.GetComponent<Image>(); button.interactable = !busy;
                if (!accessible) body.GetComponent<Image>().color = Color.Lerp(Palette[v.Color], Background, .5f);
                button.onClick.AddListener(() => Dispatch(v.Id));
            }
        }
        RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool round = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            var im = go.GetComponent<Image>(); im.color = color; if (round) { im.sprite = rounded; im.type = Image.Type.Sliced; } return r;
        }
        void Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Text)); var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<Text>(); t.text = text; t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.raycastTarget = false;
        }
        void Button(Transform parent, string label, float x, float y, float w, float h, Color color, UnityEngine.Events.UnityAction action, int size = 15, bool enabled = true)
        {
            var r = Box(parent, label, x, y, w, h, color); var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>(); b.interactable = enabled; b.onClick.AddListener(action);
            Label(r, label, 0, 0, w, h, size, enabled ? (color == Panel ? Cream : Background) : Muted, TextAnchor.MiddleCenter, true);
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
        static Sprite MakeRounded()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(8 - x, x - 23), dy = Mathf.Max(8 - y, y - 23);
                float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(8.5f - distance)));
            }
            texture.Apply(); return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
        }
    }
}
