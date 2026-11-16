using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    /// <summary>A map: its objects, where the player starts, and where enemies can appear.</summary>
    public class Arena
    {
        public string name;
        public GameObject root;
        public Vector3 playerSpawn;
        public Quaternion playerRotation;
        public Vector3[] enemySpawns;
    }

    /// <summary>
    /// "Skyline": a rooftop arena on top of a tower, built from primitives. The roof is 70 x 100 m
    /// with a helipad in the middle, stair bulkheads, air-conditioning units and low walls for
    /// cover, and a raised sniper deck at each end. The parapet is low enough to jump, and falling
    /// off puts you back on your spawn.
    /// </summary>
    public static class SkyscraperArena
    {
        static readonly Color Concrete = new Color(0.36f, 0.38f, 0.42f);
        static readonly Color DarkConcrete = new Color(0.24f, 0.26f, 0.3f);
        static readonly Color Glass = new Color(0.12f, 0.2f, 0.3f);
        static readonly Color Steel = new Color(0.5f, 0.53f, 0.57f);
        static readonly Color Paint = new Color(1f, 0.62f, 0.1f);
        static readonly Color Crate = new Color(0.42f, 0.3f, 0.18f);

        const float HalfWidth = 35f;  // x
        const float HalfLength = 50f; // z

        /// <summary>Builds the arena with the middle of the roof surface at position.</summary>
        public static Arena Build(Shader litShader, Vector3 position)
        {
            var root = new GameObject("Skyline Arena");
            root.transform.position = position;
            Transform t = root.transform;
            var paint = new Painter(litShader);

            // The tower and its roof.
            Box(t, paint, "Roof", new Vector3(0f, -1f, 0f), new Vector3(HalfWidth * 2f, 2f, HalfLength * 2f), Concrete);
            Box(t, paint, "Tower", new Vector3(0f, -152f, 0f), new Vector3(HalfWidth * 2f - 2f, 300f, HalfLength * 2f - 2f), Glass);

            // Parapet: low enough to hop over, with the corners left open.
            const float lip = 0.6f;
            Box(t, paint, "Parapet North", new Vector3(0f, lip * 0.5f, HalfLength - 0.25f), new Vector3(HalfWidth * 2f - 12f, lip, 0.5f), DarkConcrete);
            Box(t, paint, "Parapet South", new Vector3(0f, lip * 0.5f, -HalfLength + 0.25f), new Vector3(HalfWidth * 2f - 12f, lip, 0.5f), DarkConcrete);
            Box(t, paint, "Parapet East", new Vector3(HalfWidth - 0.25f, lip * 0.5f, 0f), new Vector3(0.5f, lip, HalfLength * 2f - 12f), DarkConcrete);
            Box(t, paint, "Parapet West", new Vector3(-HalfWidth + 0.25f, lip * 0.5f, 0f), new Vector3(0.5f, lip, HalfLength * 2f - 12f), DarkConcrete);

            // Helipad in the middle: a low disc you can run across.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Helipad";
            pad.transform.SetParent(t, false);
            pad.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            pad.transform.localScale = new Vector3(18f, 0.1f, 18f);
            paint.Apply(pad, DarkConcrete);
            Object.Destroy(pad.GetComponent<Collider>()); // the default collider is a capsule
            pad.AddComponent<MeshCollider>();
            Box(t, paint, "Helipad Mark A", new Vector3(-1.6f, 0.21f, 0f), new Vector3(0.6f, 0.02f, 6f), Paint, false);
            Box(t, paint, "Helipad Mark B", new Vector3(1.6f, 0.21f, 0f), new Vector3(0.6f, 0.02f, 6f), Paint, false);
            Box(t, paint, "Helipad Mark C", new Vector3(0f, 0.21f, 0f), new Vector3(3.2f, 0.02f, 0.6f), Paint, false);

            // Stair bulkheads, with crates to climb onto them.
            Bulkhead(t, paint, new Vector3(-22f, 0f, -30f), 1f);
            Bulkhead(t, paint, new Vector3(22f, 0f, 30f), -1f);

            // Air-conditioning units: chest-high cover.
            Vector3[] units =
            {
                new Vector3(-12f, 0f, -12f), new Vector3(12f, 0f, 12f), new Vector3(-10f, 0f, 18f),
                new Vector3(10f, 0f, -18f), new Vector3(-25f, 0f, 10f), new Vector3(25f, 0f, -10f)
            };
            foreach (Vector3 u in units)
            {
                Box(t, paint, "AC Unit", u + new Vector3(0f, 0.8f, 0f), new Vector3(3f, 1.6f, 2f), Steel);
                Box(t, paint, "AC Fan", u + new Vector3(0f, 1.65f, 0f), new Vector3(1.6f, 0.1f, 1.6f), DarkConcrete, false);
            }

            // Low walls that break up the long sight lines.
            Box(t, paint, "Wall", new Vector3(-6f, 1f, -28f), new Vector3(8f, 2f, 0.5f), DarkConcrete);
            Box(t, paint, "Wall", new Vector3(6f, 1f, 28f), new Vector3(8f, 2f, 0.5f), DarkConcrete);
            Box(t, paint, "Wall", new Vector3(-18f, 1f, 0f), new Vector3(0.5f, 2f, 10f), DarkConcrete);
            Box(t, paint, "Wall", new Vector3(18f, 1f, 0f), new Vector3(0.5f, 2f, 10f), DarkConcrete);

            // A raised deck at each end, reached by a ramp.
            Deck(t, paint, -43f, -1f);
            Deck(t, paint, 43f, 1f);

            // Water tanks on stilts in two corners.
            Tank(t, paint, new Vector3(26f, 0f, -38f));
            Tank(t, paint, new Vector3(-26f, 0f, 38f));

            Box(t, paint, "Antenna", new Vector3(30f, 10f, 45f), new Vector3(0.6f, 20f, 0.6f), Steel);
            Box(t, paint, "Antenna Light", new Vector3(30f, 20.3f, 45f), new Vector3(0.9f, 0.6f, 0.9f), Paint, false);

            // The skyline around it. Scenery only.
            Box(t, paint, "Tower West", new Vector3(-95f, -60f, 20f), new Vector3(50f, 300f, 50f), Glass);
            Box(t, paint, "Tower East", new Vector3(100f, -140f, -40f), new Vector3(60f, 300f, 60f), DarkConcrete);
            Box(t, paint, "Tower North", new Vector3(20f, -180f, 135f), new Vector3(70f, 300f, 50f), Glass);
            Box(t, paint, "Tower South", new Vector3(-30f, -120f, -145f), new Vector3(60f, 300f, 60f), DarkConcrete);
            Box(t, paint, "Tower Far", new Vector3(150f, -100f, 110f), new Vector3(45f, 300f, 45f), Glass);

            Vector3[] spawns =
            {
                new Vector3(-20f, 0f, 30f), new Vector3(0f, 0f, 36f), new Vector3(20f, 0f, 20f),
                new Vector3(-10f, 0f, 12f), new Vector3(10f, 0f, 24f), new Vector3(-28f, 0f, 24f),
                new Vector3(28f, 0f, 38f), new Vector3(-14f, 0f, 40f), new Vector3(14f, 0f, 6f),
                new Vector3(-4f, 0f, 20f), new Vector3(-4f, 3f, 43f), new Vector3(5f, 3f, 44f),
                new Vector3(-26f, 0f, -4f), new Vector3(26f, 0f, 4f)
            };
            for (int i = 0; i < spawns.Length; i++) spawns[i] = t.TransformPoint(spawns[i]);

            return new Arena
            {
                name = "Skyline",
                root = root,
                playerSpawn = t.TransformPoint(new Vector3(0f, 0.1f, -34f)),
                playerRotation = Quaternion.identity, // facing north, up the roof
                enemySpawns = spawns
            };
        }

        // A stairwell box with two crates as steps onto its roof. side is +1 or -1: which way the crates sit.
        static void Bulkhead(Transform t, Painter paint, Vector3 at, float side)
        {
            Box(t, paint, "Bulkhead", at + new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 10f), DarkConcrete);
            Box(t, paint, "Bulkhead Door", at + new Vector3(0f, 1.1f, -5.02f * side), new Vector3(1.4f, 2.2f, 0.06f), Steel, false);
            Box(t, paint, "Crate", at + new Vector3(5.5f * side, 0.9f, 2f), new Vector3(3f, 1.8f, 3f), Crate);
            Box(t, paint, "Crate", at + new Vector3(5f * side, 2.7f, 2f), new Vector3(2f, 1.8f, 2f), Crate);
        }

        // A deck 3 m up across the end of the roof. dir is +1 for the north end, -1 for the south.
        static void Deck(Transform t, Painter paint, float z, float dir)
        {
            Box(t, paint, "Deck", new Vector3(0f, 2.85f, z), new Vector3(16f, 0.3f, 8f), Steel);
            Box(t, paint, "Deck Rail", new Vector3(0f, 3.4f, z - 3.9f * dir), new Vector3(16f, 0.8f, 0.2f), DarkConcrete);
            foreach (float px in new[] { -7.5f, 7.5f })
            {
                foreach (float pz in new[] { -3.5f, 3.5f })
                    Box(t, paint, "Deck Leg", new Vector3(px, 1.35f, z + pz), new Vector3(0.5f, 2.7f, 0.5f), DarkConcrete);
            }
            // One ramp per deck, on opposite sides so the two ends mirror each other.
            Ramp(t, paint, new Vector3(-17f * dir, 0f, z), new Vector3(-8f * dir, 3f, z), 4f);
        }

        static void Tank(Transform t, Painter paint, Vector3 at)
        {
            GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = "Water Tank";
            tank.transform.SetParent(t, false);
            tank.transform.localPosition = at + new Vector3(0f, 5f, 0f);
            tank.transform.localScale = new Vector3(5f, 2f, 5f);
            paint.Apply(tank, Steel);
            foreach (float lx in new[] { -1.6f, 1.6f })
            {
                foreach (float lz in new[] { -1.6f, 1.6f })
                    Box(t, paint, "Tank Leg", at + new Vector3(lx, 1.5f, lz), new Vector3(0.3f, 3f, 0.3f), DarkConcrete);
            }
        }

        // A sloped walkway from one point to another. Both points are on its top surface.
        static void Ramp(Transform t, Painter paint, Vector3 from, Vector3 to, float width)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ramp";
            go.transform.SetParent(t, false);
            Vector3 along = to - from;
            go.transform.localRotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, 0.3f, along.magnitude);
            go.transform.localPosition = (from + to) * 0.5f - go.transform.localRotation * Vector3.up * 0.15f;
            paint.Apply(go, Steel);
        }

        static GameObject Box(Transform t, Painter paint, string name, Vector3 centre, Vector3 size, Color color, bool solid = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(t, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            paint.Apply(go, color);
            if (!solid) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // One shared material per colour.
        class Painter
        {
            readonly Shader shader;
            readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

            public Painter(Shader shader) { this.shader = shader; }

            public void Apply(GameObject go, Color color)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r == null) return;
                if (shader == null)
                {
                    r.material.color = color;
                    return;
                }
                if (!materials.TryGetValue(color, out Material m))
                {
                    m = new Material(shader) { color = color };
                    materials[color] = m;
                }
                r.sharedMaterial = m;
            }
        }
    }
}
