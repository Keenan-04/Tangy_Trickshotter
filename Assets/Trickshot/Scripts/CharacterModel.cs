using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// TEMPLATE. The player's character for the menu: a figure built from boxes, holding a sniper
    /// rifle across the chest. Swap this for a real rigged model later; keep SetColors (or replace
    /// it with whatever applies the equipped outfit and rifle finish).
    /// </summary>
    public class CharacterModel : MonoBehaviour
    {
        [Tooltip("Degrees the figure turns either side while idling.")]
        public float idleTurn = 14f;
        public float idleSpeed = 0.5f;

        static readonly Color Skin = new Color(0.87f, 0.68f, 0.53f);
        static readonly Color Dark = new Color(0.08f, 0.08f, 0.1f);

        Material outfitMaterial;
        Material rifleMaterial;
        Material skinMaterial;
        Material darkMaterial;
        Quaternion baseRotation;

        /// <summary>Builds the figure standing at the origin of its own transform, facing +Z.</summary>
        public static CharacterModel Build(Shader litShader)
        {
            var model = new GameObject("Character").AddComponent<CharacterModel>();
            model.Assemble(litShader);
            return model;
        }

        void Assemble(Shader litShader)
        {
            if (litShader != null)
            {
                outfitMaterial = new Material(litShader);
                rifleMaterial = new Material(litShader);
                skinMaterial = new Material(litShader) { color = Skin };
                darkMaterial = new Material(litShader) { color = Dark };
            }

            Transform body = transform;
            Part(body, PrimitiveType.Cube, "Left Leg", new Vector3(-0.12f, 0.45f, 0f), new Vector3(0.19f, 0.9f, 0.22f), Vector3.zero, outfitMaterial);
            Part(body, PrimitiveType.Cube, "Right Leg", new Vector3(0.12f, 0.45f, 0f), new Vector3(0.19f, 0.9f, 0.22f), Vector3.zero, outfitMaterial);
            Part(body, PrimitiveType.Cube, "Boots", new Vector3(0f, 0.06f, 0.04f), new Vector3(0.46f, 0.12f, 0.32f), Vector3.zero, darkMaterial);
            Part(body, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.6f, 0.27f), Vector3.zero, outfitMaterial);
            Part(body, PrimitiveType.Cube, "Vest", new Vector3(0f, 1.24f, 0.02f), new Vector3(0.44f, 0.4f, 0.3f), Vector3.zero, darkMaterial);
            Part(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.66f, 0f), Vector3.one * 0.26f, Vector3.zero, skinMaterial);
            Part(body, PrimitiveType.Cube, "Cap", new Vector3(0f, 1.76f, 0.02f), new Vector3(0.27f, 0.08f, 0.3f), Vector3.zero, darkMaterial);

            // Arms reach forward to the rifle.
            Part(body, PrimitiveType.Cube, "Left Arm", new Vector3(-0.31f, 1.24f, 0.13f), new Vector3(0.13f, 0.5f, 0.13f), new Vector3(-55f, 0f, 14f), outfitMaterial);
            Part(body, PrimitiveType.Cube, "Right Arm", new Vector3(0.31f, 1.18f, 0.11f), new Vector3(0.13f, 0.5f, 0.13f), new Vector3(-40f, 0f, -14f), outfitMaterial);

            // The rifle is built pointing along its own +Z, then tilted across the chest, muzzle up to the left.
            Transform rifle = new GameObject("Rifle").transform;
            rifle.SetParent(body, false);
            rifle.localPosition = new Vector3(0.04f, 1.14f, 0.27f);
            rifle.localRotation = Quaternion.Euler(-32f, -74f, 0f);
            Part(rifle, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.1f, 0.7f), Vector3.zero, rifleMaterial);
            Part(rifle, PrimitiveType.Cube, "Barrel", new Vector3(0f, 0.02f, 0.62f), new Vector3(0.03f, 0.03f, 0.6f), Vector3.zero, darkMaterial);
            Part(rifle, PrimitiveType.Cube, "Stock", new Vector3(0f, -0.03f, -0.46f), new Vector3(0.05f, 0.13f, 0.26f), Vector3.zero, rifleMaterial);
            Part(rifle, PrimitiveType.Cube, "Scope", new Vector3(0f, 0.095f, 0.05f), new Vector3(0.05f, 0.05f, 0.3f), Vector3.zero, darkMaterial);
            Part(rifle, PrimitiveType.Cube, "Magazine", new Vector3(0f, -0.1f, 0.06f), new Vector3(0.04f, 0.1f, 0.09f), Vector3.zero, darkMaterial);

            SetColors(Profile.Equipped(ItemSlot.Outfit).color, Profile.Equipped(ItemSlot.RifleFinish).color);
        }

        void Start()
        {
            baseRotation = transform.localRotation;
        }

        void Update()
        {
            // A slow turn from side to side so the menu is not a still picture.
            float yaw = Mathf.Sin(Time.unscaledTime * idleSpeed) * idleTurn;
            transform.localRotation = baseRotation * Quaternion.Euler(0f, yaw, 0f);
        }

        void OnDestroy()
        {
            if (outfitMaterial != null) Destroy(outfitMaterial);
            if (rifleMaterial != null) Destroy(rifleMaterial);
            if (skinMaterial != null) Destroy(skinMaterial);
            if (darkMaterial != null) Destroy(darkMaterial);
        }

        public void SetColors(Color outfit, Color rifleFinish)
        {
            if (outfitMaterial != null) outfitMaterial.color = outfit;
            if (rifleMaterial != null) rifleMaterial.color = rifleFinish;
        }

        static void Part(Transform parent, PrimitiveType shape, string name, Vector3 position, Vector3 size, Vector3 euler, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null) r.sharedMaterial = material;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}
