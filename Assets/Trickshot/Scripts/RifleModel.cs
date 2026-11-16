using UnityEngine;

namespace Trickshot
{
    /// <summary>How one rifle handles, and which model it uses.</summary>
    public class WeaponStats
    {
        public string model;          // file name under Resources/Rifles
        public string blurb;          // one line for the menu
        public float barrelLength;    // metres from grip to muzzle, used to check the model's scale
        public float adsTime;         // seconds to scope in
        public float boltTime;        // seconds between shots
        public float hipSpread;       // degrees, unscoped
        public float scopedFov;       // smaller = more zoom
        public float sway;            // degrees of scope drift
        public float recoil;          // degrees of kick
        public float moveScale;       // walking speed multiplier while carrying it
        public bool boltSound = true; // false for self-loading rifles
    }

    /// <summary>
    /// A rifle model loaded from Resources/Rifles and stood upright: grip at this object's origin,
    /// barrel along +Z, top along +Y. The FBX files carry three marker objects (Muzzle, Top, Eye)
    /// and this class uses them to work out which way the model was exported, so the game does not
    /// depend on the exporter's axis settings.
    /// </summary>
    public class RifleModel : MonoBehaviour
    {
        /// <summary>Tip of the barrel.</summary>
        public Transform muzzle;
        /// <summary>Where the shooter's eye goes, in this object's local space.</summary>
        public Vector3 eyeLocal = new Vector3(0f, 0.16f, -0.2f);

        Material finish, metal, polymer, lens;

        /// <summary>Loads and prepares a rifle under parent. Returns null if the model file is missing.</summary>
        public static RifleModel Spawn(WeaponStats weapon, Transform parent, Shader litShader)
        {
            if (weapon == null) return null;
            GameObject prefab = Resources.Load<GameObject>("Rifles/" + weapon.model);
            if (prefab == null) return null;

            var holder = new GameObject(weapon.model + " Model");
            var model = holder.AddComponent<RifleModel>();
            GameObject instance = Instantiate(prefab);
            instance.name = weapon.model;
            Transform t = instance.transform;
            t.SetParent(holder.transform, true);

            Transform muzzleMark = Find(t, "Muzzle");
            Transform topMark = Find(t, "Top");
            Transform eyeMark = Find(t, "Eye");

            if (muzzleMark != null && topMark != null)
            {
                Vector3 up = topMark.position - t.position;
                Vector3 toMuzzle = muzzleMark.position - t.position;
                Vector3 forward = toMuzzle - Vector3.Project(toMuzzle, up); // the muzzle sits a little above the grip
                if (up.sqrMagnitude > 1e-8f && forward.sqrMagnitude > 1e-8f)
                {
                    t.rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * t.rotation;
                    if (weapon.barrelLength > 0f) t.localScale *= weapon.barrelLength / forward.magnitude;
                }
            }
            t.localPosition = Vector3.zero;

            model.muzzle = muzzleMark;
            if (eyeMark != null) model.eyeLocal = holder.transform.InverseTransformPoint(eyeMark.position);
            model.AssignMaterials(instance, litShader);

            foreach (Collider c in instance.GetComponentsInChildren<Collider>()) Destroy(c);

            holder.transform.SetParent(parent, false);
            return model;
        }

        // The model has four material slots, named in Blender. Swap each for one this script owns.
        void AssignMaterials(GameObject instance, Shader litShader)
        {
            if (litShader == null) return;
            finish = new Material(litShader) { color = new Color(0.12f, 0.12f, 0.14f) };
            metal = new Material(litShader) { color = new Color(0.07f, 0.07f, 0.08f) };
            polymer = new Material(litShader) { color = new Color(0.03f, 0.03f, 0.035f) };
            lens = new Material(litShader) { color = new Color(0.2f, 0.65f, 1f) };
            if (metal.HasProperty("_Metallic")) metal.SetFloat("_Metallic", 0.8f);

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = mats[i] != null ? mats[i].name : "";
                    if (n.Contains("Metal")) mats[i] = metal;
                    else if (n.Contains("Polymer")) mats[i] = polymer;
                    else if (n.Contains("Lens")) mats[i] = lens;
                    else mats[i] = finish;
                }
                r.sharedMaterials = mats;
            }
        }

        public void SetFinish(Color color)
        {
            if (finish != null) finish.color = color;
        }

        void OnDestroy()
        {
            if (finish != null) Destroy(finish);
            if (metal != null) Destroy(metal);
            if (polymer != null) Destroy(polymer);
            if (lens != null) Destroy(lens);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name.StartsWith(name)) return child;
            }
            return null;
        }
    }
}
