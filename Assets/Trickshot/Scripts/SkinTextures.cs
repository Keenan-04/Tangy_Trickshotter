using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Makes the patterned rifle skins. Each pattern is a small tiling texture painted in code,
    /// so there are no image files. The rifle models are UV-mapped so a texture repeats every 20 cm.
    /// </summary>
    public static class SkinTextures
    {
        const int Size = 128;

        static Texture2D[] cache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache = null; }

        /// <summary>The texture for a pattern, or null for a plain colour.</summary>
        public static Texture2D Get(SkinPattern pattern)
        {
            if (pattern == SkinPattern.None) return null;
            if (cache == null) cache = new Texture2D[System.Enum.GetValues(typeof(SkinPattern)).Length];
            int i = (int)pattern;
            if (cache[i] == null) cache[i] = Paint(pattern);
            return cache[i];
        }

        /// <summary>How fast the pattern drifts across the rifle, in tiles per second. Most do not move.</summary>
        public static Vector2 Scroll(SkinPattern pattern)
        {
            switch (pattern)
            {
                case SkinPattern.DarkMatter: return new Vector2(0.03f, 0.05f);
                case SkinPattern.SunBlaze: return new Vector2(0f, 0.12f);
                default: return Vector2.zero;
            }
        }

        /// <summary>Sets a URP Lit material up to show a skin: colour or texture, shine and glow.</summary>
        public static void Apply(Material m, ShopItem skin)
        {
            if (m == null || skin == null) return;

            Texture2D texture = Get(skin.pattern);
            m.mainTexture = texture;
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            m.color = texture != null ? Color.white : skin.color;

            float metallic = 0.2f, smoothness = 0.5f, glow = 0f;
            switch (skin.pattern)
            {
                case SkinPattern.CarbonFiber: metallic = 0.6f; smoothness = 0.8f; break;
                case SkinPattern.BlueberryDream: metallic = 0.1f; smoothness = 0.75f; break;
                case SkinPattern.SunBlaze: metallic = 0.3f; smoothness = 0.6f; glow = 0.9f; break;
                case SkinPattern.DarkMatter: metallic = 0.5f; smoothness = 0.85f; glow = 1.6f; break;
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);

            // Glowing skins use their own picture as the glow, so the bright parts of the pattern light up.
            if (m.HasProperty("_EmissionColor"))
            {
                if (glow > 0f && texture != null)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", texture);
                    m.SetColor("_EmissionColor", Color.white * glow);
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        static Texture2D Paint(SkinPattern pattern)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            texture.name = pattern.ToString();
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = x / (float)Size;
                    float v = y / (float)Size;
                    Color c;
                    switch (pattern)
                    {
                        case SkinPattern.CarbonFiber: c = Carbon(u, v); break;
                        case SkinPattern.BlueberryDream: c = Blueberry(u, v, x, y); break;
                        case SkinPattern.SunBlaze: c = Blaze(u, v); break;
                        default: c = DarkMatter(u, v, x, y); break;
                    }
                    c.a = 1f;
                    pixels[y * Size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // A 2x2 twill weave: strands run across in one square and up in the next, each rounded like a thread.
        static Color Carbon(float u, float v)
        {
            const int cells = 8;
            float fu = u * cells, fv = v * cells;
            int cu = Mathf.FloorToInt(fu), cv = Mathf.FloorToInt(fv);
            bool across = ((cu + cv) & 1) == 0;
            float along = across ? fv - cv : fu - cu;
            float thread = Mathf.Sin(Mathf.PI * along * 3f % Mathf.PI);
            float shade = 0.06f + 0.16f * Mathf.Abs(thread) * (across ? 1f : 0.7f);
            return new Color(shade, shade, shade * 1.08f);
        }

        // Soft clouds of blue and violet with pale pink highlights and dark berry speckles.
        static Color Blueberry(float u, float v, int x, int y)
        {
            float n = Fbm(u, v, 3, 11);
            Color c = Color.Lerp(new Color(0.1f, 0.16f, 0.7f), new Color(0.55f, 0.3f, 0.9f), Mathf.SmoothStep(0.25f, 0.7f, n));
            c = Color.Lerp(c, new Color(1f, 0.72f, 0.92f), Mathf.SmoothStep(0.68f, 0.85f, n));

            // Round speckles on a coarse grid, some cells left empty.
            const int grid = 8;
            float gu = u * grid, gv = v * grid;
            int cx = Mathf.FloorToInt(gu), cy = Mathf.FloorToInt(gv);
            if (Hash(cx, cy, 23) > 0.55f)
            {
                float dx = gu - cx - 0.3f - 0.4f * Hash(cx, cy, 5);
                float dy = gv - cy - 0.3f - 0.4f * Hash(cx, cy, 6);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                c = Color.Lerp(new Color(0.05f, 0.05f, 0.3f), c, Mathf.SmoothStep(0.1f, 0.16f, d));
            }
            return c;
        }

        // Licks of flame: deep red through orange to yellow, brightest where the noise peaks.
        static Color Blaze(float u, float v)
        {
            float n = Fbm(u, v, 3, 37);
            float wave = 0.5f + 0.5f * Mathf.Sin((v + n * 0.35f) * Mathf.PI * 2f);
            float heat = Mathf.Clamp01(n * 0.9f + wave * 0.45f - 0.15f);
            Color c = Color.Lerp(new Color(0.45f, 0.02f, 0f), new Color(1f, 0.4f, 0f), Mathf.SmoothStep(0.15f, 0.55f, heat));
            return Color.Lerp(c, new Color(1f, 0.92f, 0.35f), Mathf.SmoothStep(0.6f, 0.9f, heat));
        }

        // Near-black with violet and teal gas clouds and a scatter of stars.
        static Color DarkMatter(float u, float v, int x, int y)
        {
            float gas = Fbm(u, v, 4, 71);
            float second = Fbm(u, v, 3, 113);
            gas = Mathf.Pow(Mathf.Clamp01(gas * 1.5f - 0.35f), 2f);
            Color c = new Color(0.015f, 0.01f, 0.03f);
            c += new Color(0.55f, 0.1f, 0.95f) * gas;
            c += new Color(0.05f, 0.55f, 0.7f) * Mathf.Pow(Mathf.Clamp01(second * 1.6f - 0.75f), 2f);

            float star = Hash(x, y, 197);
            if (star > 0.992f) c += Color.white * (0.5f + 0.5f * Hash(x, y, 3));
            return c;
        }

        // ------------------------------------------------------------------ tiling noise

        // Layered value noise that wraps at the texture edges, so the pattern tiles without a seam.
        static float Fbm(float u, float v, int octaves, int seed)
        {
            float total = 0f, weight = 0f, amplitude = 1f;
            int period = 4;
            for (int o = 0; o < octaves; o++)
            {
                total += Value(u * period, v * period, period, seed + o * 17) * amplitude;
                weight += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return total / weight;
        }

        static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Wrap(int i, int period) => ((i % period) + period) % period;

        // A repeatable pseudo-random number from 0 to 1 for a grid point.
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
