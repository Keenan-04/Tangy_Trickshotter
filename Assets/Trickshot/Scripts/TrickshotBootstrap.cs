using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Builds a playable test range in code: a tall tower to jump off, targets, the player,
    /// the rifle, scoring and the HUD. Nothing is saved into the scene.
    /// Once you build a real level, wire the same components up by hand and remove this.
    /// </summary>
    public class TrickshotBootstrap : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2; // built-in layer, so the rifle never hits the player or the gun

        [Header("Range")]
        public float towerHeight = 24f;
        public int targetCount = 14;

        static readonly Color GroundColor = new Color(0.22f, 0.24f, 0.28f);
        static readonly Color BlockColor = new Color(0.42f, 0.45f, 0.52f);
        static readonly Color TargetColor = new Color(1f, 0.55f, 0.05f);
        static readonly Color MovingTargetColor = new Color(1f, 0.2f, 0.25f);
        static readonly Color GunColor = new Color(0.12f, 0.12f, 0.14f);

        Shader litShader;

        void Awake()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");

            if (ScoreManager.Instance == null) gameObject.AddComponent<ScoreManager>();

            BuildRange();
            PlayerController player = BuildPlayer(out SniperRifle rifle);

            var hud = gameObject.AddComponent<TrickshotHUD>();
            hud.player = player;
            hud.rifle = rifle;
        }

        void BuildRange()
        {
            Transform root = new GameObject("Trickshot Range").transform;

            Block(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(400f, 1f, 400f), GroundColor);
            Block(root, "Tower", new Vector3(0f, towerHeight * 0.5f, 0f), new Vector3(12f, towerHeight, 12f), BlockColor);
            Block(root, "Mid Platform", new Vector3(-20f, 6f, 6f), new Vector3(8f, 12f, 8f), BlockColor);
            Block(root, "Low Platform", new Vector3(20f, 3f, -4f), new Vector3(8f, 6f, 8f), BlockColor);

            // Targets fan out in front of the tower at mixed distances and heights.
            for (int i = 0; i < targetCount; i++)
            {
                float t = targetCount > 1 ? i / (targetCount - 1f) : 0.5f;
                float angle = Mathf.Lerp(-70f, 70f, t) * Mathf.Deg2Rad;
                float distance = 30f + (i % 4) * 18f;   // 30, 48, 66, 84 m
                float height = 2f + (i % 3) * 5f;       // 2, 7, 12 m
                bool moving = i % 3 == 2;

                var pos = new Vector3(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance);

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = moving ? "Moving Target" : "Target";
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 2f;
                Paint(go, moving ? MovingTargetColor : TargetColor);

                var target = go.AddComponent<Target>();
                if (moving)
                {
                    target.basePoints = 150;
                    target.moveAxis = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle)); // sideways from the tower
                    target.moveDistance = 5f;
                    target.moveSpeed = 1.2f;
                }
            }
        }

        PlayerController BuildPlayer(out SniperRifle rifle)
        {
            var playerGo = new GameObject("Player");
            playerGo.layer = IgnoreRaycastLayer;
            playerGo.transform.position = new Vector3(0f, towerHeight + 0.1f, -3f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            Transform pivot = new GameObject("Camera Pivot").transform;
            pivot.SetParent(playerGo.transform, false);
            pivot.localPosition = new Vector3(0f, 1.62f, 0f);

            // Reuse the scene's camera so its URP settings and audio listener carry over.
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.SetParent(pivot, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.03f;

            var player = playerGo.AddComponent<PlayerController>();
            player.cameraPivot = pivot;

            // Placeholder gun made of boxes.
            Transform gun = new GameObject("Rifle Model").transform;
            gun.SetParent(cam.transform, false);
            GunPart(gun, "Body", new Vector3(0.2f, -0.2f, 0.45f), new Vector3(0.07f, 0.1f, 0.7f));
            GunPart(gun, "Barrel", new Vector3(0.2f, -0.17f, 1.05f), new Vector3(0.03f, 0.03f, 0.6f));
            GunPart(gun, "Scope", new Vector3(0.2f, -0.105f, 0.45f), new Vector3(0.05f, 0.05f, 0.28f));
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = new Vector3(0.2f, -0.17f, 1.35f);

            rifle = cam.gameObject.AddComponent<SniperRifle>();
            rifle.cam = cam;
            rifle.player = player;
            rifle.viewModel = gun.gameObject;
            rifle.muzzle = muzzle;

            return player;
        }

        GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            Paint(go, color);
            return go;
        }

        void GunPart(Transform parent, string name, Vector3 localPosition, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = IgnoreRaycastLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            Paint(go, GunColor);

            // The gun must not block the player or bullets.
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                Destroy(col);
            }
        }

        void Paint(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;

            if (litShader != null)
            {
                r.sharedMaterial = new Material(litShader) { color = color };
            }
            else
            {
                r.material.color = color; // falls back to whatever material the primitive came with
            }
        }
    }
}
