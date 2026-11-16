using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Prototype HUD drawn with OnGUI so it needs no canvas or prefabs:
    /// score, live spin and scope readouts, the scope overlay, and a popup for each shot.
    /// Replace it with a proper UI once the rules feel right.
    /// </summary>
    public class TrickshotHUD : MonoBehaviour
    {
        public PlayerController player;
        public SniperRifle rifle;
        public float popupDuration = 2.5f;

        static readonly Color Tangy = new Color(1f, 0.62f, 0.1f);
        static readonly Color Good = new Color(0.55f, 1f, 0.45f);
        static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.7f);

        Texture2D scopeTexture;
        GUIStyle big, mid, small;
        ShotResult lastShot;
        ScoreManager subscribedTo;

        void Start()
        {
            BuildScopeTexture();
            Subscribe();
        }

        void Subscribe()
        {
            if (subscribedTo != null || ScoreManager.Instance == null) return;
            subscribedTo = ScoreManager.Instance;
            subscribedTo.ShotResolved += OnShot;
        }

        void OnDestroy()
        {
            if (subscribedTo != null) subscribedTo.ShotResolved -= OnShot;
            if (scopeTexture != null) Destroy(scopeTexture);
        }

        void OnShot(ShotResult result)
        {
            lastShot = result;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Subscribe();

            float w = Screen.width;
            float h = Screen.height;
            float ui = h / 1080f;
            EnsureStyles(ui);

            DrawReticle(w, h, ui);

            ScoreManager score = ScoreManager.Instance;
            if (score != null)
            {
                DrawScore(score, ui);
                DrawLiveReadout(score, w, h, ui);
                DrawPopup(w, h, ui);
            }

            Label(new Rect(24f * ui, h - 60f * ui, w, 40f * ui),
                "WASD move   Shift sprint   Space jump   RMB scope   LMB fire   R back to the top   [ ] sensitivity   Esc free cursor",
                small, TextAnchor.UpperLeft, Dim);

            if (player != null && !player.InputActive)
                Label(new Rect(0f, h * 0.6f, w, 60f * ui), "CLICK TO PLAY", mid, TextAnchor.UpperCenter, Color.white);
        }

        void DrawReticle(float w, float h, float ui)
        {
            if (rifle != null && rifle.ShowScopeOverlay && scopeTexture != null)
            {
                float size = h;
                var r = new Rect((w - size) * 0.5f, 0f, size, size);
                GUI.color = Color.white;
                GUI.DrawTexture(r, scopeTexture);
                GUI.color = Color.black;
                if (r.xMin > 0f)
                {
                    GUI.DrawTexture(new Rect(0f, 0f, r.xMin + 1f, h), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - 1f, 0f, w - r.xMax + 1f, h), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                return;
            }

            // Small hip-fire dot. Real trickshotters play without one, so delete this for hard mode.
            float d = Mathf.Max(4f, 5f * ui);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect((w - d) * 0.5f - 1f, (h - d) * 0.5f - 1f, d + 2f, d + 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect((w - d) * 0.5f, (h - d) * 0.5f, d, d), Texture2D.whiteTexture);
        }

        void DrawScore(ScoreManager score, float ui)
        {
            Label(new Rect(24f * ui, 18f * ui, 900f * ui, 80f * ui), score.Score.ToString("N0"), big, TextAnchor.UpperLeft, Tangy);

            string accuracy = score.ShotsFired > 0 ? Mathf.RoundToInt(100f * score.ShotsHit / score.ShotsFired) + "%" : "-";
            Label(new Rect(26f * ui, 92f * ui, 900f * ui, 40f * ui),
                "Best shot " + score.BestShot.ToString("N0") + "    Hits " + score.ShotsHit + "/" + score.ShotsFired + "  (" + accuracy + ")",
                small, TextAnchor.UpperLeft, Dim);
        }

        // Shows what the next shot is worth right now, so you can see the scope multiplier drain.
        void DrawLiveReadout(ScoreManager score, float w, float h, float ui)
        {
            if (player == null || rifle == null) return;

            bool scoped = rifle.IsScoped;
            float scopeMult = score.ScopeMultiplier(scoped, rifle.ScopeTime);
            float spinMult = score.SpinMultiplier(player.SpinDegrees);
            float airMult = 1f + (player.IsAirborne ? score.airborneBonus : 0f);

            string scopeText;
            Color scopeColor;
            if (!scoped)
            {
                scopeText = "NO SCOPE  x" + scopeMult.ToString("0.0");
                scopeColor = Good;
            }
            else if (score.IsQuickscope(rifle.ScopeTime))
            {
                scopeText = "QUICKSCOPE  " + rifle.ScopeTime.ToString("0.00") + "s  x" + scopeMult.ToString("0.0");
                scopeColor = Tangy;
            }
            else
            {
                scopeText = "HARDSCOPE  " + rifle.ScopeTime.ToString("0.00") + "s  x" + scopeMult.ToString("0.0");
                scopeColor = Bad;
            }

            float y = h - 190f * ui;
            Label(new Rect(0f, y, w, 40f * ui), scopeText, mid, TextAnchor.UpperCenter, scopeColor);

            string spinText = "SPIN  " + Mathf.FloorToInt(player.SpinDegrees) + "°  x" + spinMult.ToString("0.0");
            Label(new Rect(0f, y + 38f * ui, w, 40f * ui), spinText, mid, TextAnchor.UpperCenter,
                spinMult > 1f ? Good : Dim);

            string total = "NEXT SHOT  x" + (scopeMult * spinMult * airMult).ToString("0.0") + (player.IsAirborne ? "  (airborne)" : "");
            Label(new Rect(0f, y + 76f * ui, w, 40f * ui), total, small, TextAnchor.UpperCenter, Dim);
        }

        void DrawPopup(float w, float h, float ui)
        {
            if (lastShot == null) return;
            float age = Time.time - lastShot.time;
            if (age < 0f || age > popupDuration) return;

            float alpha = Mathf.Clamp01((popupDuration - age) / 0.6f);
            float rise = Mathf.Min(age, 0.25f) / 0.25f * 20f * ui;
            float y = h * 0.28f - rise;

            if (!lastShot.hit)
            {
                Label(new Rect(0f, y, w, 80f * ui), "MISS", mid, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.6f * alpha));
                return;
            }

            Color c = lastShot.points >= 0 ? Tangy : Bad;
            c.a = alpha;
            string sign = lastShot.points >= 0 ? "+" : "";
            Label(new Rect(0f, y, w, 80f * ui), sign + lastShot.points.ToString("N0"), big, TextAnchor.UpperCenter, c);

            string tags = string.Join("   ·   ", lastShot.tags);
            Label(new Rect(0f, y + 74f * ui, w, 40f * ui), tags, mid, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, alpha));

            string math = lastShot.basePoints + "  x" + lastShot.scopeMultiplier.ToString("0.0") + " scope  x"
                + lastShot.spinMultiplier.ToString("0.0") + " spin  x" + lastShot.bonusMultiplier.ToString("0.0") + " style";
            Label(new Rect(0f, y + 112f * ui, w, 40f * ui), math, small, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.7f * alpha));
        }

        void Label(Rect r, string text, GUIStyle style, TextAnchor anchor, Color color)
        {
            style.alignment = anchor;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.85f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        void EnsureStyles(float ui)
        {
            if (big == null)
            {
                big = MakeStyle();
                mid = MakeStyle();
                small = MakeStyle();
            }
            big.fontSize = Mathf.Max(12, Mathf.RoundToInt(64f * ui));
            mid.fontSize = Mathf.Max(10, Mathf.RoundToInt(28f * ui));
            small.fontSize = Mathf.Max(9, Mathf.RoundToInt(20f * ui));
        }

        static GUIStyle MakeStyle()
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontStyle = FontStyle.Bold;
            s.wordWrap = false;
            s.richText = false;
            s.clipping = TextClipping.Overflow;
            s.normal.textColor = Color.white;
            return s;
        }

        // A black square with a see-through circle and crosshair lines, generated in code.
        void BuildScopeTexture()
        {
            const int n = 512;
            scopeTexture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            scopeTexture.wrapMode = TextureWrapMode.Clamp;
            scopeTexture.filterMode = FilterMode.Bilinear;

            var pixels = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            float radius = n * 0.47f;
            float postStart = n * 0.14f;

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Solid black outside the lens, with a 2px soft edge.
                    float a = Mathf.Clamp01((dist - (radius - 2f)) / 2f);

                    float ax = Mathf.Abs(dx);
                    float ay = Mathf.Abs(dy);
                    bool hair = ax < 0.75f || ay < 0.75f;                                  // thin centre lines
                    bool post = (ax < 2.5f && ay > postStart) || (ay < 2.5f && ax > postStart); // thick outer posts
                    if (hair || post) a = 1f;

                    pixels[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }

            scopeTexture.SetPixels32(pixels);
            scopeTexture.Apply(false);
        }
    }
}
