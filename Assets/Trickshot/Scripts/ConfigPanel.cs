using UnityEngine;
using UnityEngine.UI;

namespace Trickshot
{
    /// <summary>
    /// A settings board that stands on the range. Look at a control and click it:
    /// the strafing switch flips, sliders follow your aim while the button is held,
    /// and START begins a round of the chosen length. It is drawn on a world-space
    /// canvas and works out what you are pointing at itself, so the cursor stays locked.
    /// </summary>
    public class ConfigPanel : MonoBehaviour
    {
        /// <summary>True while the player is aiming at the board. The rifle does not fire then.</summary>
        public static bool IsPointedAt { get; private set; }

        public AimTrainer trainer;
        [Tooltip("How close you must be to use the board, in metres.")]
        public float useDistance = 15f;

        [Header("Slider ranges")]
        [Tooltip("Strafe speed in metres per second.")]
        public Vector2 strafeSpeedRange = new Vector2(1f, 8f);
        [Tooltip("Head size as a multiple of the normal head.")]
        public Vector2 headScaleRange = new Vector2(0.5f, 3f);
        [Tooltip("Round length in seconds.")]
        public Vector2 roundSecondsRange = new Vector2(15f, 180f);

        // The canvas is laid out in these units, x from the left and y from the top.
        const float Width = 1000f;
        const float Height = 720f;
        const float MetresPerUnit = 0.003f; // 3 m wide, about 2.2 m tall

        const float LabelX = 50f;
        const float ControlX = 400f;
        const float TrackWidth = 400f;
        const float RowHeight = 80f;

        static readonly Color Board = new Color(0.07f, 0.08f, 0.1f, 0.94f);
        static readonly Color Track = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color Tangy = new Color(1f, 0.62f, 0.1f);
        static readonly Color On = new Color(0.3f, 0.75f, 0.3f);
        static readonly Color Off = new Color(0.45f, 0.47f, 0.52f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);

        class PanelSlider
        {
            public Rect area;            // where a click grabs it
            public RectTransform fill;
            public RectTransform handle;
            public Text valueText;
            public float min, max, step, value;
            public System.Func<float, string> format;
            public System.Action<float> changed;
        }

        Font font;
        bool built;
        Rect toggleRect;
        Rect startRect;
        Image toggleImage;
        Text toggleText;
        Text startText;
        RectTransform cursor;
        PanelSlider speedSlider, headSlider, roundSlider;
        PanelSlider dragging;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { IsPointedAt = false; }

        void Start()
        {
            if (trainer == null) trainer = AimTrainer.Instance;
            if (trainer == null) return;
            Build();
        }

        void OnDisable()
        {
            IsPointedAt = false;
            dragging = null;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // keeps the text sharp up close

            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Width, Height);
            root.localScale = Vector3.one * MetresPerUnit;

            Box("Board", 0f, 0f, Width, Height, Board);
            Box("Top Edge", 0f, 0f, Width, 10f, Tangy);
            Label("RANGE SETTINGS", 0f, 26f, Width, 64f, 46, TextAnchor.MiddleCenter, Color.white);

            // Strafing on/off
            float y = 120f;
            Label("Strafing", LabelX, y, 340f, RowHeight, 36, TextAnchor.MiddleLeft, Color.white);
            toggleRect = new Rect(ControlX, y + 6f, 250f, RowHeight - 12f);
            toggleImage = Box("Strafing Switch", toggleRect.x, toggleRect.y, toggleRect.width, toggleRect.height, Off).GetComponent<Image>();
            toggleText = Label("", toggleRect.x, toggleRect.y, toggleRect.width, toggleRect.height, 36, TextAnchor.MiddleCenter, Color.white);

            speedSlider = Slider("Strafe speed", 225f, strafeSpeedRange, 0.5f, trainer.strafeSpeed,
                v => v.ToString("0.0") + " m/s",
                v => trainer.SetStrafeSpeed(v));

            headSlider = Slider("Head size", 330f, headScaleRange, 0.1f, trainer.headScale,
                v => Mathf.RoundToInt(Target.HeadDiameter * 100f * v) + " cm",
                v => trainer.SetHeadScale(v));

            roundSlider = Slider("Round length", 435f, roundSecondsRange, 5f, trainer.roundSeconds,
                v => Mathf.RoundToInt(v) + " s",
                v => trainer.roundSeconds = v);

            startRect = new Rect(LabelX, 545f, Width - LabelX * 2f, 96f);
            Box("Start Button", startRect.x, startRect.y, startRect.width, startRect.height, Tangy);
            startText = Label("", startRect.x, startRect.y, startRect.width, startRect.height, 42, TextAnchor.MiddleCenter, Color.black);

            Label("Look at a control and click.  Hold and drag the sliders.", 0f, 656f, Width, 50f, 26, TextAnchor.MiddleCenter, Dim);

            cursor = Box("Cursor", 0f, 0f, 14f, 14f, Color.white);
            cursor.gameObject.SetActive(false);

            built = true;
            RefreshText();
        }

        void Update()
        {
            if (!built) return;

            bool pointing = FindPointer(out Vector2 p);
            IsPointedAt = pointing;
            cursor.gameObject.SetActive(pointing);
            RefreshText(); // the 1, 2 and 3 keys change the mode and the strafing default

            if (!pointing)
            {
                dragging = null;
                return;
            }

            cursor.anchoredPosition = new Vector2(p.x - 7f, -(p.y - 7f));

            if (TSInput.FirePressed)
            {
                if (toggleRect.Contains(p)) trainer.SetStrafing(!trainer.strafing);
                else if (startRect.Contains(p)) trainer.StartRound();
                else if (speedSlider.area.Contains(p)) dragging = speedSlider;
                else if (headSlider.area.Contains(p)) dragging = headSlider;
                else if (roundSlider.area.Contains(p)) dragging = roundSlider;
            }

            if (!TSInput.FireHeld) dragging = null;
            if (dragging != null) SetFromPointer(dragging, p.x);
        }

        // Where the centre of the screen meets the board, in canvas units. False if the player is not aiming at it.
        bool FindPointer(out Vector2 point)
        {
            point = default;

            Camera cam = Camera.main;
            PlayerController player = PlayerController.Instance;
            if (cam == null || player == null || !player.InputActive) return false;

            var plane = new Plane(-transform.forward, transform.position);
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!plane.Raycast(ray, out float enter) || enter > useDistance) return false;

            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(enter));
            point = new Vector2(local.x + Width * 0.5f, Height * 0.5f - local.y);

            // While a slider is held you can drift off the board without letting go of it.
            if (dragging != null) return true;
            return point.x >= 0f && point.x <= Width && point.y >= 0f && point.y <= Height;
        }

        void SetFromPointer(PanelSlider s, float pointerX)
        {
            float t = Mathf.Clamp01((pointerX - ControlX) / TrackWidth);
            float v = Mathf.Lerp(s.min, s.max, t);
            if (s.step > 0f) v = Mathf.Round(v / s.step) * s.step;
            v = Mathf.Clamp(v, s.min, s.max);
            if (Mathf.Approximately(v, s.value)) return;

            s.value = v;
            s.changed(v);
            ShowSlider(s);
        }

        void ShowSlider(PanelSlider s)
        {
            float t = Mathf.InverseLerp(s.min, s.max, s.value);
            s.fill.sizeDelta = new Vector2(TrackWidth * t, s.fill.sizeDelta.y);
            s.handle.anchoredPosition = new Vector2(ControlX + TrackWidth * t - 8f, s.handle.anchoredPosition.y);
            s.valueText.text = s.format(s.value);
        }

        void RefreshText()
        {
            bool strafing = trainer.strafing;
            toggleImage.color = strafing ? On : Off;
            string toggle = strafing ? "ON" : "OFF";
            if (toggleText.text != toggle) toggleText.text = toggle;

            string start = "START  " + AimTrainer.ModeName(trainer.Mode) + "  ROUND";
            if (startText.text != start) startText.text = start;
        }

        PanelSlider Slider(string label, float y, Vector2 range, float step, float value,
            System.Func<float, string> format, System.Action<float> changed)
        {
            Label(label, LabelX, y, 340f, RowHeight, 36, TextAnchor.MiddleLeft, Color.white);

            var s = new PanelSlider
            {
                area = new Rect(ControlX - 20f, y, TrackWidth + 40f, RowHeight),
                min = range.x,
                max = Mathf.Max(range.y, range.x + 0.001f),
                step = step,
                format = format,
                changed = changed
            };
            s.value = Mathf.Clamp(value, s.min, s.max);

            Box(label + " Track", ControlX, y + 28f, TrackWidth, 24f, Track);
            s.fill = Box(label + " Fill", ControlX, y + 28f, 0f, 24f, Tangy);
            s.handle = Box(label + " Handle", ControlX, y + 12f, 16f, 56f, Color.white);
            s.valueText = Label("", ControlX + TrackWidth + 30f, y, 200f, RowHeight, 34, TextAnchor.MiddleLeft, Color.white);

            ShowSlider(s);
            return s;
        }

        RectTransform Box(string name, float x, float y, float w, float h, Color color)
        {
            RectTransform rt = Element(name, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        Text Label(string text, float x, float y, float w, float h, int size, TextAnchor anchor, Color color)
        {
            RectTransform rt = Element("Text", x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        // Places a child using top-left coordinates, so the layout above reads like a page.
        RectTransform Element(string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }
    }
}
