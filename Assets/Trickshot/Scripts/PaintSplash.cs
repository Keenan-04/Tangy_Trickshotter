using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum Team
    {
        Neutral, // training dummies
        Red,
        Blue
    }

    /// <summary>
    /// Paintball-style splatter. A burst throws blobs of paint that fall, and each one flattens into
    /// a splat where it lands. Splats stay for a while, then shrink away.
    /// One of these objects is one burst; call PaintSplash.Burst to make one.
    /// </summary>
    public class PaintSplash : MonoBehaviour
    {
        const float Gravity = -18f;
        const float Linger = 14f;   // seconds a splat stays
        const float Fade = 1.5f;    // seconds it takes to shrink away
        const float MaxFlight = 5f; // blobs still in the air after this are removed

        class Blob
        {
            public Transform t;
            public Vector3 velocity;
            public float size;
            public bool landed;
            public Vector3 splatScale;
        }

        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        readonly List<Blob> blobs = new List<Blob>();
        float age;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { materials.Clear(); }

        public static Color ColorOf(Team team)
        {
            switch (team)
            {
                case Team.Red: return new Color(0.92f, 0.08f, 0.1f);
                case Team.Blue: return new Color(0.1f, 0.35f, 1f);
                default: return new Color(1f, 0.62f, 0.1f);
            }
        }

        /// <summary>Throws paint out from a point. More blobs and more speed make a bigger mess.</summary>
        public static void Burst(Vector3 at, Color color, int count, float speed)
        {
            var splash = new GameObject("Paint Splash").AddComponent<PaintSplash>();
            splash.transform.position = at;
            Material material = MaterialFor(color);

            for (int i = 0; i < count; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Paint";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(splash.transform, false);

                var blob = new Blob { t = go.transform, size = Random.Range(0.06f, 0.16f) };
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.15f; // mostly outwards and up
                blob.velocity = direction.normalized * speed * Random.Range(0.35f, 1f);
                blob.t.localScale = Vector3.one * blob.size;

                Renderer r = go.GetComponent<Renderer>();
                if (material != null) r.sharedMaterial = material;
                else r.material.color = color;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                splash.blobs.Add(blob);
            }
        }

        static Material MaterialFor(Color color)
        {
            if (materials.TryGetValue(color, out Material m) && m != null) return m;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;
            m = new Material(lit) { color = color };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f); // wet paint
            materials[color] = m;
            return m;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            if (age > Linger + Fade)
            {
                Destroy(gameObject);
                return;
            }

            float shrink = age > Linger ? 1f - (age - Linger) / Fade : 1f;

            foreach (Blob b in blobs)
            {
                if (b.t == null) continue;

                if (b.landed)
                {
                    if (shrink < 1f) b.t.localScale = b.splatScale * shrink;
                    continue;
                }

                if (age > MaxFlight)
                {
                    Destroy(b.t.gameObject);
                    b.t = null;
                    continue;
                }

                b.velocity.y += Gravity * dt;
                Vector3 step = b.velocity * dt;
                float distance = step.magnitude;

                if (distance > 0.0001f
                    && Physics.Raycast(b.t.position, step / distance, out RaycastHit hit, distance + b.size * 0.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponent<HitZone>() == null) // paint passes through other dummies
                {
                    // Flatten against whatever it hit. Faster blobs make wider splats.
                    float spread = b.size * Random.Range(2.5f, 4.5f);
                    b.splatScale = new Vector3(spread, 0.012f, spread * Random.Range(0.7f, 1.3f));
                    b.t.position = hit.point + hit.normal * 0.008f;
                    b.t.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    b.t.localScale = b.splatScale;
                    b.landed = true;
                }
                else
                {
                    b.t.position += step;
                }
            }
        }
    }
}
