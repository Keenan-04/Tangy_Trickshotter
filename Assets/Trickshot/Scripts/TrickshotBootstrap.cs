using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// Builds the whole game in code: the range with its back wall, the player, the rifle, scoring,
    /// the trainer and its settings panel, the template match modes, the HUD, and the main menu
    /// with its character stage. The game opens on the menu. Nothing is saved into the scene.
    /// Once you build a real level, wire the same components up by hand and remove this.
    /// </summary>
    public class TrickshotBootstrap : MonoBehaviour
    {
        const int IgnoreRaycastLayer = 2; // built-in layer, so the rifle never hits the player or the gun

        [Header("Range")]
        public float wallDistance = 70f;
        public float wallHeight = 14f;
        public float[] distanceLines = { 15f, 30f, 45f };

        [Header("Maps")]
        [Tooltip("Middle of the Skyline roof. High up and well away from the range.")]
        public Vector3 skylinePosition = new Vector3(600f, 250f, 600f);

        [Header("Menu")]
        [Tooltip("Where the menu's character stage is built. Keep it out of sight of the range.")]
        public Vector3 menuStagePosition = new Vector3(0f, 0f, -100f);

        static readonly Color GroundColor = new Color(0.22f, 0.24f, 0.28f);
        static readonly Color BlockColor = new Color(0.42f, 0.45f, 0.52f);
        static readonly Color LineColor = new Color(0.34f, 0.37f, 0.43f);
        static readonly Color BackdropColor = new Color(0.1f, 0.11f, 0.14f);
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

            // Main game modes (templates). They share the range until they get maps of their own.
            var deathmatch = gameObject.AddComponent<TeamDeathmatchMode>();
            var elimination = gameObject.AddComponent<EliminationMode>();

            // Team Deathmatch is played on the rooftop. It is built far from the range and kept
            // switched off until a match starts.
            Arena skyline = SkyscraperArena.Build(litShader, skylinePosition);
            skyline.root.SetActive(false);
            deathmatch.arena = skyline;

            // Everything starts switched off: the menu turns on whatever the chosen mode needs.
            trainer.enabled = false;
            panelGo.SetActive(false);
            player.enabled = false;
            rifle.enabled = false;
            hud.enabled = false;

            BuildMenuStage(out Camera menuCamera, out CharacterModel character);

            var menu = gameObject.AddComponent<MainMenu>();
            menu.player = player;
            menu.rifle = rifle;
            menu.hud = hud;
            menu.trainer = trainer;
            menu.matchModes = new MatchMode[] { deathmatch, elimination };
            menu.trainingProps = panelGo;
            menu.rangeSpawn = player.transform.position;
            menu.menuCamera = menuCamera;
            menu.character = character;
        }

        // A small stage well behind the range where the menu shows the player's character.
        void BuildMenuStage(out Camera menuCamera, out CharacterModel character)
        {
            Vector3 centre = menuStagePosition;
            Transform root = new GameObject("Menu Stage").transform;

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Podium";
            disc.transform.SetParent(root, false);
            disc.transform.position = centre + Vector3.up * 0.04f;
            disc.transform.localScale = new Vector3(2.6f, 0.04f, 2.6f);
            Paint(disc, BlockColor);

            Block(root, "Backdrop", centre + new Vector3(1.1f, 4f, -3f), new Vector3(18f, 8f, 0.5f), BackdropColor);

            character = CharacterModel.Build(litShader);
            character.transform.SetParent(root, false);
            character.transform.position = centre + Vector3.up * 0.08f;
            character.transform.rotation = Quaternion.Euler(0f, 16f, 0f);

            // The camera looks past the character's side, which puts the figure on the right of the screen
            // and leaves the left free for the menu.
            var camGo = new GameObject("Menu Camera");
            camGo.transform.SetParent(root, false);
            camGo.transform.position = centre + new Vector3(1.1f, 1.3f, 3.6f);
            camGo.transform.rotation = Quaternion.Euler(4f, 180f, 0f);
            menuCamera = camGo.AddComponent<Camera>();
            menuCamera.fieldOfView = 40f;
            menuCamera.nearClipPlane = 0.1f;
            menuCamera.enabled = false;

            var lightGo = new GameObject("Menu Key Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = centre + new Vector3(-1.5f, 2.6f, 3f);
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 12f;
            keyLight.intensity = 22f;
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
