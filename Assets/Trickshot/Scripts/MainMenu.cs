using UnityEngine;

namespace Trickshot
{
    /// <summary>
    /// The front end: modes down the left, the player's character on the right, and tabs for
    /// Play, Inventory and Store. It also switches between the menu and a running mode:
    /// in the menu the player, rifle and HUD are off and a separate camera looks at the character.
    /// Press M in a mode to come back here.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public PlayerController player;
        public SniperRifle rifle;
        public TrickshotHUD hud;
        public AimTrainer trainer;
        [Tooltip("The main game modes, listed under Training.")]
        public MatchMode[] matchModes = new MatchMode[0];
        [Tooltip("Things that only belong on the range in Training, such as the settings panel.")]
        public GameObject trainingProps;
        public Camera menuCamera;
        public CharacterModel character;
        public string gameTitle = "TANGY TRICKSHOTTER";
        [Tooltip("Where the player starts on the training range.")]
        public Vector3 rangeSpawn = new Vector3(0f, 0.1f, 0f);
        [Tooltip("Where the first-person rifle's grip sits, relative to the camera.")]
        public Vector3 viewModelHip = new Vector3(0.17f, -0.2f, 0.3f);

        public bool InMenu { get; private set; }

        enum Tab { Play, Inventory, Store }

        static readonly string[] TabNames = { "PLAY", "INVENTORY", "STORE" };
        static readonly string[] TrainingRules =
        {
            "Flick, strafe and quickscope drills against dummies",
            "Timed rounds with score, headshot rate and accuracy",
            "Settings panel on the range: strafing, head size, round length",
            "Round score earns credits for the store",
            "Map: training range"
        };

        static readonly Color Tangy = new Color(1f, 0.62f, 0.1f);
        static readonly Color Panel = new Color(0.05f, 0.06f, 0.08f, 0.72f);
        static readonly Color Row = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color RowSelected = new Color(1f, 0.62f, 0.1f, 0.3f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.65f);
        static readonly Color Good = new Color(0.3f, 0.75f, 0.3f);

        Tab tab = Tab.Play;
        int selected; // 0 = Training, then matchModes in order
        GUIStyle style;
        float ui = 1f;
        ItemSlot shownSlot = ItemSlot.Rifle;
        RifleModel viewRifle;
        string shownWeaponId;
        bool fallbackSaved;
        Transform fallbackMuzzle;
        Vector3 fallbackAdsOffset;

        int ModeCount => 1 + matchModes.Length;

        void Start()
        {
            Profile.Changed += ApplyLoadout;
            ApplyLoadout();
            ShowMenu();
        }

        void OnDestroy()
        {
            Profile.Changed -= ApplyLoadout;
        }

        void Update()
        {
            if (!InMenu)
            {
                if (TSInput.MenuPressed) ShowMenu();
                return;
            }

            // Keyboard works as well as the mouse on the Play tab.
            if (tab != Tab.Play) return;
            int pressed = TSInput.ModePressed;
            if (pressed >= 0 && pressed < ModeCount) selected = pressed;
            if (TSInput.ConfirmPressed) Play(selected);
        }

        /// <summary>Stops whatever is running and shows the menu.</summary>
        public void ShowMenu()
        {
            InMenu = true;
            StopModes();

            if (player != null) player.enabled = false;
            if (rifle != null)
            {
                rifle.enabled = false;
                if (rifle.cam != null) rifle.cam.enabled = false;
            }
            if (hud != null) hud.enabled = false;
            if (menuCamera != null) menuCamera.enabled = true;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Leaves the menu and starts a mode. 0 is Training, then matchModes in order.</summary>
        public void Play(int mode)
        {
            if (mode < 0 || mode >= ModeCount) return;
            InMenu = false;
            StopModes();
            ApplyLoadout();

            if (menuCamera != null) menuCamera.enabled = false;
            if (rifle != null)
            {
                if (rifle.cam != null) rifle.cam.enabled = true;
                rifle.enabled = true;
            }
            if (hud != null) hud.enabled = true;

            // Training and any mode without a map of its own use the range.
            Arena arena = mode > 0 ? matchModes[mode - 1].arena : null;
            if (arena != null && arena.root != null) arena.root.SetActive(true);

            if (player != null)
            {
                if (arena != null) player.SetSpawn(arena.playerSpawn, arena.playerRotation);
                else player.SetSpawn(rangeSpawn, Quaternion.identity);
                player.enabled = true;
                player.Respawn();
                player.SetCursorLocked(true);
            }

            if (mode == 0)
            {
                if (trainingProps != null) trainingProps.SetActive(true);
                if (trainer != null) trainer.enabled = true;
            }
            else
            {
                MatchMode match = matchModes[mode - 1];
                match.enabled = true;
                match.Begin();
            }
        }

        void StopModes()
        {
            if (trainer != null) trainer.enabled = false;
            if (trainingProps != null) trainingProps.SetActive(false);
            foreach (MatchMode m in matchModes)
            {
                if (m == null) continue;
                m.enabled = false;
                if (m.arena != null && m.arena.root != null) m.arena.root.SetActive(false);
            }
        }

        // Puts the equipped rifle, finish and outfit on the menu character and on the player.
        void ApplyLoadout()
        {
            Color outfit = Profile.Equipped(ItemSlot.Outfit).color;
            ShopItem skin = Profile.Equipped(ItemSlot.RifleFinish);
            Color finish = skin.color;
            ShopItem rifleItem = Profile.Equipped(ItemSlot.Rifle);
            WeaponStats weapon = rifleItem != null ? rifleItem.weapon : null;

            if (character != null) character.SetLoadout(outfit, skin, weapon, Profile.Equipped(ItemSlot.Hat));
            if (rifle == null) return;

            rifle.ApplyWeapon(weapon);
            if (player != null) player.WeaponMoveScale = weapon != null ? weapon.moveScale : 1f;
            if (rifle.viewModel == null) return;

            if (!fallbackSaved)
            {
                fallbackSaved = true;
                fallbackMuzzle = rifle.muzzle;
                fallbackAdsOffset = rifle.viewModelAdsOffset;
            }

            // Swap the first-person rifle only when a different one is equipped.
            string id = rifleItem != null ? rifleItem.id : "";
            if (id != shownWeaponId)
            {
                shownWeaponId = id;
                if (viewRifle != null) Destroy(viewRifle.gameObject);
                viewRifle = RifleModel.Spawn(weapon, rifle.viewModel.transform, Shader.Find("Universal Render Pipeline/Lit"));

                foreach (Transform child in rifle.viewModel.transform)
                {
                    bool isModel = viewRifle != null && child == viewRifle.transform;
                    if (!isModel) child.gameObject.SetActive(viewRifle == null); // the box rifle is the fallback
                }

                if (viewRifle != null)
                {
                    viewRifle.transform.localPosition = viewModelHip;
                    rifle.muzzle = viewRifle.muzzle;
                    rifle.viewModelAdsOffset = -(viewModelHip + viewRifle.eyeLocal); // brings the scope to the eye
                }
                else
                {
                    rifle.muzzle = fallbackMuzzle;
                    rifle.viewModelAdsOffset = fallbackAdsOffset;
                }
            }

            if (viewRifle != null)
            {
                viewRifle.SetSkin(skin);
            }
            else
            {
                foreach (Renderer r in rifle.viewModel.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial != null) r.sharedMaterial.color = finish;
                }
            }
        }

        string ModeName(int i) => i == 0 ? "TRAINING" : matchModes[i - 1].DisplayName;
        string ModeSummary(int i) => i == 0 ? "Sniper aim trainer. Warm up before a match." : matchModes[i - 1].Summary;
        string[] ModeRules(int i) => i == 0 ? TrainingRules : matchModes[i - 1].Rules;

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            if (!InMenu) return;

            float w = Screen.width;
            float h = Screen.height;
            ui = h / 1080f;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
                style.normal.textColor = Color.white;
                style.hover.textColor = Color.white;
                style.active.textColor = Color.white;
            }

            // Dark column behind the left side so the text reads over the 3D scene.
            Fill(new Rect(0f, 0f, 900f * ui, h), Panel);
            Fill(new Rect(0f, 0f, 900f * ui, 8f * ui), Tangy);

            Text(R(70f, 44f, 800f, 70f), gameTitle, 54f, TextAnchor.MiddleLeft, Color.white);

            for (int i = 0; i < TabNames.Length; i++)
            {
                bool current = (int)tab == i;
                Rect r = R(70f + i * 250f, 140f, 236f, 56f);
                if (Button(r, TabNames[i], 26f, current ? Tangy : Row, current ? Color.black : Color.white)) tab = (Tab)i;
            }

            Text(new Rect(w - 420f * ui, 44f * ui, 380f * ui, 40f * ui), "CREDITS  " + Profile.Credits.ToString("N0"),
                28f, TextAnchor.MiddleRight, Tangy);

            switch (tab)
            {
                case Tab.Play: DrawPlay(h); break;
                case Tab.Inventory: DrawInventory(); break;
                case Tab.Store: DrawStore(); break;
            }

            // Name plate under the character.
            Text(new Rect(w - 620f * ui, h - 150f * ui, 580f * ui, 30f * ui), "YOUR OPERATOR", 20f, TextAnchor.MiddleRight, Dim);
            Text(new Rect(w - 620f * ui, h - 118f * ui, 580f * ui, 40f * ui),
                Profile.Equipped(ItemSlot.Rifle).name + "   /   " + Profile.Equipped(ItemSlot.RifleFinish).name,
                26f, TextAnchor.MiddleRight, Color.white);
        }

        void DrawPlay(float h)
        {
            float y = 236f;
            for (int i = 0; i < ModeCount; i++)
            {
                Rect r = R(70f, y, 760f, 104f);
                bool current = i == selected;
                if (Button(r, "", 20f, current ? RowSelected : Row, Color.white)) selected = i;
                if (current) Fill(new Rect(r.x, r.y, 8f * ui, r.height), Tangy);

                Text(R(100f, y + 12f, 560f, 44f), ModeName(i), 34f, TextAnchor.MiddleLeft, Color.white);
                Text(R(100f, y + 56f, 700f, 34f), ModeSummary(i), 20f, TextAnchor.MiddleLeft, Dim);
                if (i > 0) Text(R(610f, y + 14f, 200f, 36f), "TEMPLATE", 18f, TextAnchor.MiddleRight, Tangy);
                y += 120f;
            }

            y += 14f;
            foreach (string rule in ModeRules(selected))
            {
                Text(R(84f, y, 760f, 32f), "-  " + rule, 20f, TextAnchor.MiddleLeft, Color.white);
                y += 34f;
            }

            if (selected > 0)
            {
                Text(new Rect(70f * ui, h - 226f * ui, 760f * ui, 28f * ui),
                    "Online matchmaking is built but not switched on yet. This plays offline.", 17f, TextAnchor.MiddleLeft, Dim);
            }

            Rect play = new Rect(70f * ui, h - 190f * ui, 760f * ui, 92f * ui);
            if (Button(play, "PLAY  " + ModeName(selected), 36f, Tangy, Color.black)) Play(selected);
            Text(new Rect(70f * ui, h - 86f * ui, 760f * ui, 30f * ui), "1 2 3  select        ENTER  play        M  back to this menu",
                18f, TextAnchor.MiddleLeft, Dim);
        }

        // Rifle / skin / outfit / hat switch shared by the Inventory and Store tabs.
        void DrawSlotChips(float y)
        {
            int i = 0;
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                bool current = slot == shownSlot;
                if (Button(R(70f + i * 192f, y, 184f, 44f), SlotName(slot), 20f, current ? RowSelected : Row, Color.white)) shownSlot = slot;
                i++;
            }
        }

        // The equipped rifle's level, with a bar for progress to the next one.
        void DrawWeaponLevel(float y)
        {
            ShopItem rifle = Profile.Equipped(ItemSlot.Rifle);
            int level = Profile.WeaponLevel(rifle);
            string max = level >= Profile.MaxWeaponLevel ? "  (MAX)" : "";
            Text(R(70f, y, 400f, 32f), rifle.name + "    LEVEL " + level + max, 22f, TextAnchor.MiddleLeft, Color.white);
            Fill(R(470f, y + 11f, 360f, 10f), Row);
            Fill(R(470f, y + 11f, 360f * Profile.LevelProgress(rifle), 10f), Tangy);
        }

        void DrawInventory()
        {
            string hint = shownSlot == ItemSlot.RifleFinish
                ? "Patterned skins unlock as the equipped rifle levels up. Kills earn weapon XP."
                : "What you own. Click to equip.";
            Text(R(70f, 212f, 760f, 28f), hint, 18f, TextAnchor.MiddleLeft, Dim);
            DrawSlotChips(250f);

            float y = 312f;
            if (shownSlot == ItemSlot.Rifle || shownSlot == ItemSlot.RifleFinish)
            {
                DrawWeaponLevel(y);
                y += 46f;
            }

            ShopItem worn = Profile.Equipped(shownSlot);
            foreach (ShopItem item in Profile.Catalog)
            {
                if (item.slot != shownSlot || !Profile.Owns(item)) continue;
                ItemRow(item, y);

                Rect b = R(630f, y + 7f, 190f, 42f);
                if (!Profile.Unlocked(item)) Button(b, "LEVEL " + item.level, 20f, Row, Color.white, false);
                else if (item == worn) Button(b, "EQUIPPED", 20f, Good, Color.white, false);
                else if (Button(b, "EQUIP", 20f, Tangy, Color.black)) Profile.Equip(item);
                y += 62f;
            }
        }

        void DrawStore()
        {
            string hint = shownSlot == ItemSlot.RifleFinish
                ? "Plain colours are sold here. Patterned skins are earned: see Inventory."
                : "Earn credits in Training and by winning matches.";
            Text(R(70f, 212f, 760f, 28f), hint, 18f, TextAnchor.MiddleLeft, Dim);
            DrawSlotChips(250f);

            float y = 312f;
            bool any = false;
            foreach (ShopItem item in Profile.Catalog)
            {
                if (item.slot != shownSlot || item.price <= 0) continue;
                any = true;
                ItemRow(item, y);

                Rect b = R(630f, y + 7f, 190f, 42f);
                if (Profile.Owns(item)) Button(b, "OWNED", 20f, Row, Color.white, false);
                else if (Button(b, item.price.ToString("N0"), 22f, Tangy, Color.black, Profile.Credits >= item.price)) Profile.Buy(item);
                y += 62f;
            }
            if (!any) Text(R(70f, y, 760f, 40f), "Nothing to buy here yet.", 20f, TextAnchor.MiddleLeft, Dim);
        }

        // Background, swatch and name of one item. The caller adds the button on the right.
        void ItemRow(ShopItem item, float y)
        {
            Fill(R(70f, y, 760f, 56f), Row);

            Rect swatch = R(82f, y + 7f, 42f, 42f);
            Texture2D pattern = SkinTextures.Get(item.pattern);
            if (pattern != null) GUI.DrawTexture(swatch, pattern);
            else Fill(swatch, item.color);

            Text(R(144f, y, 320f, 56f), item.name, 24f, TextAnchor.MiddleLeft, Color.white);
            if (item.weapon != null)
                Text(R(290f, y, 330f, 56f), item.weapon.blurb + "   Lv " + Profile.WeaponLevel(item), 16f, TextAnchor.MiddleLeft, Dim);
            else if (item.level > 1)
                Text(R(400f, y, 220f, 56f), "Weapon level " + item.level, 16f, TextAnchor.MiddleLeft, Dim);
        }

        static string SlotName(ItemSlot slot)
        {
            switch (slot)
            {
                case ItemSlot.Rifle: return "RIFLE";
                case ItemSlot.RifleFinish: return "SKIN";
                case ItemSlot.Outfit: return "OUTFIT";
                default: return "HAT";
            }
        }

        // A rectangle given in 1080p units, scaled to the real screen.
        Rect R(float x, float y, float w, float h) => new Rect(x * ui, y * ui, w * ui, h * ui);

        void Fill(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void Text(Rect r, string text, float size, TextAnchor anchor, Color color)
        {
            style.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * ui));
            style.alignment = anchor;
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        bool Button(Rect r, string text, float size, Color background, Color textColor, bool usable = true)
        {
            bool hover = usable && r.Contains(Event.current.mousePosition);
            Color fill = hover ? Color.Lerp(background, Color.white, 0.2f) : background;
            if (!usable) fill.a *= 0.55f;
            Fill(r, fill);

            style.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * ui));
            style.alignment = TextAnchor.MiddleCenter;
            GUI.color = usable ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            bool clicked = GUI.Button(r, text, style) && usable;
            GUI.color = Color.white;

            if (clicked) Sfx.Play(SfxId.Click, 0.6f);
            return clicked;
        }
    }
}
