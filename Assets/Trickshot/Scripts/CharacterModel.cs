using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// The player's character for the menu: a thick stick figure holding the equipped rifle across
    /// the chest, wearing the equipped hat. The outfit colours the whole figure.
    /// </summary>
    public class CharacterModel : MonoBehaviour
    {
        [Tooltip("Degrees the figure turns either side while idling.")]
        public float idleTurn = 14f;
        public float idleSpeed = 0.5f;

        static readonly Color Dark = new Color(0.08f, 0.08f, 0.1f);

        Material bodyMaterial;
        Material rifleMaterial; // for the fallback box rifle only
        Material darkMaterial;
        Material hatMaterial;
        Material trimMaterial;
        Quaternion baseRotation;
        Shader lit;
        Transform rifleHolder;
        Transform hatHolder;
        RifleModel rifleModel;
        string shownModel;
        HatStyle shownHat = (HatStyle)(-1);

        /// <summary>Builds the figure standing at the origin of its own transform, facing +Z.</summary>
        public static CharacterModel Build(Shader litShader)
        {
            var model = new GameObject("Character").AddComponent<CharacterModel>();
            model.Assemble(litShader);
            return model;
        }

        void Assemble(Shader litShader)
        {
            lit = litShader;
            if (litShader != null)
            {
                bodyMaterial = new Material(litShader);
                rifleMaterial = new Material(litShader);
                darkMaterial = new Material(litShader) { color = Dark };
                hatMaterial = new Material(litShader);
                trimMaterial = new Material(litShader) { color = Color.white };
            }

            Transform t = transform;
            float sw = StickFigure.ShoulderHalfWidth;
            float hw = StickFigure.HipHalfWidth;

            StickFigure.Ball(t, "Head", new Vector3(0f, StickFigure.HeadCentreHeight, 0f), StickFigure.HeadDiameter, bodyMaterial, false);
            StickFigure.Limb(t, "Body", StickFigure.Hip, StickFigure.Chest, StickFigure.BodyThickness, bodyMaterial, false);
            StickFigure.Limb(t, "Shoulders", new Vector3(-sw, 1.42f, 0f), new Vector3(sw, 1.42f, 0f), 0.12f, bodyMaterial, false);
            StickFigure.Limb(t, "Hips", new Vector3(-hw, 0.93f, 0f), new Vector3(hw, 0.93f, 0f), StickFigure.LegThickness, bodyMaterial, false);

            // A wide, planted stance.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 hip = new Vector3(hw * side, 0.93f, 0f);
                Vector3 knee = new Vector3(0.16f * side, 0.5f, 0.04f);
                Vector3 foot = new Vector3(0.21f * side, 0.07f, 0f);
                StickFigure.Limb(t, "Thigh", hip, knee, StickFigure.LegThickness, bodyMaterial, false);
                StickFigure.Limb(t, "Shin", knee, foot, StickFigure.LegThickness, bodyMaterial, false);
            }

            // Right hand on the grip, left hand forward on the handguard.
            Arm(t, new Vector3(sw, 1.42f, 0f), new Vector3(0.3f, 1.16f, 0.1f), new Vector3(0.14f, 1.07f, 0.24f));
            Arm(t, new Vector3(-sw, 1.42f, 0f), new Vector3(-0.33f, 1.2f, 0.18f), new Vector3(-0.24f, 1.31f, 0.35f));

            // The rifle points along its holder's +Z, tilted across the chest with the muzzle up to the left.
            rifleHolder = new GameObject("Rifle").transform;
            rifleHolder.SetParent(t, false);
            rifleHolder.localPosition = new Vector3(0.04f, 1.14f, 0.27f);
            rifleHolder.localRotation = Quaternion.Euler(-32f, -74f, 0f);
            StickFigure.Shape(rifleHolder, PrimitiveType.Cube, "Body", Vector3.zero, new Vector3(0.06f, 0.1f, 0.7f), Vector3.zero, rifleMaterial);
            StickFigure.Shape(rifleHolder, PrimitiveType.Cube, "Barrel", new Vector3(0f, 0.02f, 0.62f), new Vector3(0.03f, 0.03f, 0.6f), Vector3.zero, darkMaterial);
            StickFigure.Shape(rifleHolder, PrimitiveType.Cube, "Scope", new Vector3(0f, 0.095f, 0.05f), new Vector3(0.05f, 0.05f, 0.3f), Vector3.zero, darkMaterial);

            hatHolder = new GameObject("Hat").transform;
            hatHolder.SetParent(t, false);
            hatHolder.localPosition = new Vector3(0f, StickFigure.HeadCentreHeight, 0f);
        }

        void Arm(Transform t, Vector3 shoulder, Vector3 elbow, Vector3 hand)
        {
            StickFigure.Limb(t, "Upper Arm", shoulder, elbow, StickFigure.ArmThickness, bodyMaterial, false);
            StickFigure.Limb(t, "Forearm", elbow, hand, StickFigure.ArmThickness, bodyMaterial, false);
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
            foreach (Material m in new[] { bodyMaterial, rifleMaterial, darkMaterial, hatMaterial, trimMaterial })
            {
                if (m != null) Destroy(m);
            }
        }

        /// <summary>Applies the equipped outfit, rifle, rifle skin and hat.</summary>
        public void SetLoadout(Color outfit, ShopItem skin, WeaponStats weapon, ShopItem hat)
        {
            if (bodyMaterial != null) bodyMaterial.color = outfit;
            if (rifleMaterial != null && skin != null) rifleMaterial.color = skin.color;

            if (rifleHolder != null)
            {
                string wanted = weapon != null ? weapon.model : "";
                if (wanted != shownModel)
                {
                    shownModel = wanted;
                    if (rifleModel != null) Destroy(rifleModel.gameObject);
                    rifleModel = RifleModel.Spawn(weapon, rifleHolder, lit);
                    if (rifleModel != null) rifleModel.transform.localPosition = new Vector3(0f, 0f, -0.12f);

                    // The box rifle only shows if the real model could not be loaded.
                    foreach (Transform child in rifleHolder)
                    {
                        bool isModel = rifleModel != null && child == rifleModel.transform;
                        if (!isModel) child.gameObject.SetActive(rifleModel == null);
                    }
                }
                if (rifleModel != null) rifleModel.SetSkin(skin);
            }

            SetHat(hat);
        }

        void SetHat(ShopItem hat)
        {
            if (hatHolder == null) return;
            HatStyle style = hat != null ? hat.hat : HatStyle.None;
            if (hatMaterial != null && hat != null) hatMaterial.color = hat.color;
            if (style == shownHat) return;
            shownHat = style;

            foreach (Transform child in hatHolder) Destroy(child.gameObject);
            BuildHat(style);
        }

        // Hats are built around the centre of the head, which is a ball 0.125 m in radius.
        void BuildHat(HatStyle style)
        {
            Transform h = hatHolder;
            Material m = hatMaterial;

            switch (style)
            {
                case HatStyle.Cap:
                    StickFigure.Shape(h, PrimitiveType.Sphere, "Dome", new Vector3(0f, 0.055f, 0f), new Vector3(0.27f, 0.17f, 0.27f), Vector3.zero, m);
                    StickFigure.Shape(h, PrimitiveType.Cube, "Brim", new Vector3(0f, 0.05f, 0.16f), new Vector3(0.2f, 0.018f, 0.16f), new Vector3(8f, 0f, 0f), m);
                    StickFigure.Shape(h, PrimitiveType.Sphere, "Button", new Vector3(0f, 0.142f, 0f), Vector3.one * 0.03f, Vector3.zero, trimMaterial);
                    break;

                case HatStyle.Beanie:
                    StickFigure.Shape(h, PrimitiveType.Sphere, "Knit", new Vector3(0f, 0.05f, 0f), new Vector3(0.275f, 0.22f, 0.275f), Vector3.zero, m);
                    StickFigure.Shape(h, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.025f, 0f), new Vector3(0.285f, 0.025f, 0.285f), Vector3.zero, trimMaterial);
                    StickFigure.Shape(h, PrimitiveType.Sphere, "Pom", new Vector3(0f, 0.19f, 0f), Vector3.one * 0.08f, Vector3.zero, trimMaterial);
                    break;

                case HatStyle.Cone:
                    StickFigure.Shape(h, PrimitiveType.Cube, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.28f, 0.02f, 0.28f), Vector3.zero, m);
                    for (int i = 0; i < 5; i++)
                    {
                        float width = Mathf.Lerp(0.2f, 0.05f, i / 4f);
                        StickFigure.Shape(h, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.14f + i * 0.07f, 0f),
                            new Vector3(width, 0.035f, width), Vector3.zero, i == 2 ? trimMaterial : m);
                    }
                    break;

                case HatStyle.TopHat:
                    StickFigure.Shape(h, PrimitiveType.Cylinder, "Brim", new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.008f, 0.36f), Vector3.zero, m);
                    StickFigure.Shape(h, PrimitiveType.Cylinder, "Crown", new Vector3(0f, 0.22f, 0f), new Vector3(0.22f, 0.12f, 0.22f), Vector3.zero, m);
                    StickFigure.Shape(h, PrimitiveType.Cylinder, "Ribbon", new Vector3(0f, 0.13f, 0f), new Vector3(0.226f, 0.02f, 0.226f), Vector3.zero, trimMaterial);
                    break;

                case HatStyle.Crown:
                    StickFigure.Shape(h, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.03f, 0.24f), Vector3.zero, m);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f;
                        Vector3 around = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.105f);
                        StickFigure.Shape(h, PrimitiveType.Cube, "Point", around + new Vector3(0f, 0.18f, 0f),
                            new Vector3(0.05f, 0.05f, 0.02f), new Vector3(0f, angle, 45f), m);
                    }
                    break;

                case HatStyle.Halo:
                    for (int i = 0; i < 16; i++)
                    {
                        Vector3 around = Quaternion.Euler(0f, i * 22.5f, 0f) * new Vector3(0f, 0f, 0.14f);
                        StickFigure.Shape(h, PrimitiveType.Sphere, "Glow", around + new Vector3(0f, 0.25f, 0f), Vector3.one * 0.05f, Vector3.zero, m);
                    }
                    break;
            }
        }
    }
}
