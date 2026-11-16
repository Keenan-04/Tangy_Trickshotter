using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Builds the aim-trainer range in code: a flat firing range with a back wall, the player,
    /// the rifle, scoring, the trainer that spawns the dummies, the settings panel, and the HUD. Nothing is saved into the scene.
    /// Once you build a real level, wire the same components up by hand and remove this.
    /// </summary>
    public class TrickshotBootstrap : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2; // built-in layer, so the rifle never hits the player or the gun

        [Header("Range")]
        public float wallDistance = 70f;
        public float wallHeight = 14f;
        public float[] distanceLines = { 15f, 30f, 45f };

        static readonly Color GroundColor = new Color(0.22f, 0.24f, 0.28f);
        static readonly Color BlockColor = new Color(0.42f, 0.45f, 0.52f);
        static readonly Color LineColor = new Color(0.34f, 0.37f, 0.43f);
        static readonly Color GunColor = new Color(0.12f, 0.12f, 0.14f);

        Shader litShader;

        void Awake()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");

            if (ScoreManager.Instance == null) gameObject.AddComponent<ScoreManager>();

            BuildRange();
            PlayerController player = BuildPlayer(out SniperRifle rifle);

            var trainer = gameObject.AddComponent<AimTrainer>();
            trainer.origin = Vector3.zero;
            trainer.forward = Vector3.forward;

            // Settings board to the front left of the firing point, just outside the arc the dummies use.
            Vector3 toPanel = Quaternion.Euler(0f, -(trainer.arcDegrees + 17f), 0f) * Vector3.forward;
            var panelGo = new GameObject("Config Panel");
            panelGo.transform.position = trainer.origin + toPanel * 6f + Vector3.up * 1.6f;
            panelGo.transform.rotation = Quaternion.LookRotation(toPanel); // its face points back at the player
            panelGo.AddComponent<ConfigPanel>().trainer = trainer;

            var hud = gameObject.AddComponent<TrickshotHUD>();
            hud.player = player;
            hud.rifle = rifle;
        }

        void BuildRange()
        {
            Transform root = new GameObject("Trainer Range").transform;

            Block(root, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(300f, 1f, 300f), GroundColor);
            Block(root, "Back Wall", new Vector3(0f, wallHeight * 0.5f, wallDistance), new Vector3(160f, wallHeight, 1f), BlockColor);

            // Something to jump off for spins, behind and to the left of the firing point.
            Block(root, "Jump Box", new Vector3(-7f, 1f, -3f), new Vector3(5f, 2f, 5f), BlockColor);

            // Painted lines so you can judge how far away a target is.
            foreach (float distance in distanceLines)
            {
                GameObject line = Block(root, distance + " m line", new Vector3(0f, 0.005f, distance), new Vector3(70f, 0.01f, 0.25f), LineColor);
                Collider col = line.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }

        PlayerController BuildPlayer(out SniperRifle rifle)
        {
            var playerGo = new GameObject("Player");
            playerGo.layer = IgnoreRaycastLayer;
            playerGo.transform.position = new Vector3(0f, 0.1f, 0f);

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
