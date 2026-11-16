using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Helpers for building the game's characters: thick stick figures made of capsules and a ball
    /// for the head. Used for the player's character, the practice dummies and the enemies.
    /// </summary>
    public static class StickFigure
    {
        // Shared proportions of a 1.8 m figure. Points are in the figure's own space, feet at the origin.
        public const float HeadDiameter = 0.25f;
        public const float HeadCentreHeight = 1.64f;
        public const float BodyThickness = 0.17f;
        public const float ArmThickness = 0.11f;
        public const float LegThickness = 0.13f;

        public static readonly Vector3 Hip = new Vector3(0f, 0.95f, 0f);
        public static readonly Vector3 Chest = new Vector3(0f, 1.42f, 0f);
        public const float ShoulderHalfWidth = 0.19f;
        public const float HipHalfWidth = 0.09f;

        /// <summary>A rounded stick from a to b. Its collider is kept only if solid is true.</summary>
        public static GameObject Limb(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material material, bool solid)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);

            Vector3 along = b - a;
            float length = along.magnitude;
            go.transform.localPosition = (a + b) * 0.5f;
            if (length > 0.0001f) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, along / length);
            // A capsule primitive is 2 units tall and 1 wide; its rounded ends reach past a and b by half the thickness.
            go.transform.localScale = new Vector3(thickness, (length + thickness) * 0.5f, thickness);

            Finish(go, material, solid);
            return go;
        }

        public static GameObject Ball(Transform parent, string name, Vector3 centre, float diameter, Material material, bool solid)
        {
            return Shape(parent, PrimitiveType.Sphere, name, centre, Vector3.one * diameter, Vector3.zero, material, solid);
        }

        /// <summary>Any primitive, placed and scaled. Used for hats.</summary>
        public static GameObject Shape(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Vector3 euler, Material material, bool solid = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Finish(go, material, solid);
            return go;
        }

        static void Finish(GameObject go, Material material, bool solid)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null) r.sharedMaterial = material;
            if (!solid)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
        }
    }
}
