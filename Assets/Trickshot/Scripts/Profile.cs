using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum ItemSlot
    {
        Rifle,
        RifleFinish,
        Outfit,
        Hat
    }

    /// <summary>Patterned rifle skins. None is a plain colour.</summary>
    public enum SkinPattern
    {
        None,
        CarbonFiber,
        BlueberryDream,
        SunBlaze,
        DarkMatter
    }

    public enum HatStyle
    {
        None,
        Cap,
        Beanie,
        Cone,
        TopHat,
        Crown,
        Halo
    }

    /// <summary>One thing that can be owned and equipped: a rifle, a rifle skin, an outfit colour or a hat.</summary>
    public class ShopItem
    {
        public string id;
        public string name;
        public ItemSlot slot;
        public Color color;
        public int price;                 // 0 = not sold; everyone has it (skins may still need a level)
        public WeaponStats weapon;        // rifles only
        public SkinPattern pattern;       // rifle skins only
        public int level = 1;             // rifle skins only: weapon level needed to use it
        public HatStyle hat;              // hats only

        public ShopItem(string id, string name, ItemSlot slot, Color color, int price)
        {
            this.id = id;
            this.name = name;
            this.slot = slot;
            this.color = color;
            this.price = price;
        }
    }

    /// <summary>
    /// The player's credits, weapon levels, owned items and loadout, saved in PlayerPrefs.
    ///
    /// Each rifle earns XP from kills made with it and levels up. Patterned skins are not sold:
    /// they unlock at a weapon level, per rifle, so a higher level means more skins to choose from.
    /// The skin you pick is remembered separately for each rifle.
    /// Prices, levels and rifle stats are first guesses.
    /// </summary>
    public static class Profile
    {
        public const int StartingCredits = 500;
        public const int MaxWeaponLevel = 15;
        public const int KillXp = 10;
        public const int HeadshotXp = 15;

        static readonly Color RifleSwatch = new Color(0.3f, 0.32f, 0.36f);

        public static readonly ShopItem[] Catalog =
        {
            new ShopItem("rifle.arbalest", "Arbalest", ItemSlot.Rifle, RifleSwatch, 0)
            {
                weapon = new WeaponStats { model = "Arbalest", blurb = "Balanced all-rounder", barrelLength = 1.02f,
                    adsTime = 0.38f, boltTime = 1f, hipSpread = 5f, scopedFov = 15f, sway = 0.25f, recoil = 2.2f, moveScale = 1f }
            },
            new ShopItem("rifle.vanta", "Vanta", ItemSlot.Rifle, RifleSwatch, 400)
            {
                weapon = new WeaponStats { model = "Vanta", blurb = "Bullpup: quick to scope, kicks hard", barrelLength = 0.76f,
                    adsTime = 0.32f, boltTime = 1.1f, hipSpread = 5.5f, scopedFov = 16f, sway = 0.3f, recoil = 2.6f, moveScale = 1f }
            },
            new ShopItem("rifle.longbow", "Longbow", ItemSlot.Rifle, RifleSwatch, 600)
            {
                weapon = new WeaponStats { model = "Longbow", blurb = "Most zoom, steadiest, slowest", barrelLength = 1.04f,
                    adsTime = 0.46f, boltTime = 1.25f, hipSpread = 6f, scopedFov = 11f, sway = 0.16f, recoil = 2f, moveScale = 0.95f }
            },
            new ShopItem("rifle.hammer", "Hammer", ItemSlot.Rifle, RifleSwatch, 900)
            {
                weapon = new WeaponStats { model = "Hammer", blurb = "Semi-auto: fast follow-ups, heavy", barrelLength = 1.17f,
                    adsTime = 0.5f, boltTime = 0.45f, hipSpread = 7f, scopedFov = 14f, sway = 0.35f, recoil = 3.4f, moveScale = 0.9f,
                    boltSound = false }
            },
            new ShopItem("rifle.lumen", "Lumen", ItemSlot.Rifle, RifleSwatch, 1200)
            {
                weapon = new WeaponStats { model = "Lumen", blurb = "Fastest scope-in, light, less zoom", barrelLength = 0.9f,
                    adsTime = 0.28f, boltTime = 0.85f, hipSpread = 4.5f, scopedFov = 17f, sway = 0.3f, recoil = 1.6f, moveScale = 1.05f }
            },

            // Plain colours: bought with credits, usable on any rifle.
            new ShopItem("finish.black", "Matte Black", ItemSlot.RifleFinish, new Color(0.12f, 0.12f, 0.14f), 0),
            new ShopItem("finish.tangy", "Tangy Orange", ItemSlot.RifleFinish, new Color(1f, 0.55f, 0.05f), 300),
            new ShopItem("finish.arctic", "Arctic White", ItemSlot.RifleFinish, new Color(0.9f, 0.93f, 0.96f), 400),
            new ShopItem("finish.lime", "Lime Shock", ItemSlot.RifleFinish, new Color(0.55f, 0.95f, 0.15f), 600),
            new ShopItem("finish.royal", "Royal Purple", ItemSlot.RifleFinish, new Color(0.5f, 0.25f, 0.85f), 800),

            // Patterned skins: earned by levelling the rifle you want them on.
            new ShopItem("skin.carbon", "Carbon Fiber", ItemSlot.RifleFinish, new Color(0.16f, 0.17f, 0.19f), 0)
                { pattern = SkinPattern.CarbonFiber, level = 3 },
            new ShopItem("skin.blueberry", "Blueberry Dream", ItemSlot.RifleFinish, new Color(0.35f, 0.3f, 0.85f), 0)
                { pattern = SkinPattern.BlueberryDream, level = 5 },
            new ShopItem("skin.sunblaze", "Sun Blaze", ItemSlot.RifleFinish, new Color(1f, 0.5f, 0.05f), 0)
                { pattern = SkinPattern.SunBlaze, level = 8 },
            new ShopItem("skin.darkmatter", "Dark Matter", ItemSlot.RifleFinish, new Color(0.3f, 0.08f, 0.5f), 0)
                { pattern = SkinPattern.DarkMatter, level = 12 },

            new ShopItem("outfit.recruit", "Recruit Grey", ItemSlot.Outfit, new Color(0.42f, 0.45f, 0.5f), 0),
            new ShopItem("outfit.desert", "Desert Tan", ItemSlot.Outfit, new Color(0.76f, 0.64f, 0.44f), 250),
            new ShopItem("outfit.night", "Night Ops", ItemSlot.Outfit, new Color(0.1f, 0.13f, 0.22f), 500),
            new ShopItem("outfit.crimson", "Crimson", ItemSlot.Outfit, new Color(0.7f, 0.1f, 0.14f), 750),

            new ShopItem("hat.none", "No Hat", ItemSlot.Hat, new Color(0.2f, 0.2f, 0.22f), 0) { hat = HatStyle.None },
            new ShopItem("hat.cap", "Ball Cap", ItemSlot.Hat, new Color(0.1f, 0.3f, 0.8f), 150) { hat = HatStyle.Cap },
            new ShopItem("hat.beanie", "Beanie", ItemSlot.Hat, new Color(0.8f, 0.15f, 0.2f), 200) { hat = HatStyle.Beanie },
            new ShopItem("hat.cone", "Traffic Cone", ItemSlot.Hat, new Color(1f, 0.45f, 0.05f), 350) { hat = HatStyle.Cone },
            new ShopItem("hat.tophat", "Top Hat", ItemSlot.Hat, new Color(0.06f, 0.06f, 0.08f), 500) { hat = HatStyle.TopHat },
            new ShopItem("hat.crown", "Crown", ItemSlot.Hat, new Color(1f, 0.8f, 0.15f), 900) { hat = HatStyle.Crown },
            new ShopItem("hat.halo", "Halo", ItemSlot.Hat, new Color(1f, 0.95f, 0.5f), 1200) { hat = HatStyle.Halo },
        };

        /// <summary>Raised whenever credits, ownership, levels or the loadout change.</summary>
        public static event System.Action Changed;

        /// <summary>Raised when a rifle reaches a new level: the rifle and the level.</summary>
        public static event System.Action<ShopItem, int> LeveledUp;

        static bool loaded;
        static int credits;
        static readonly HashSet<string> owned = new HashSet<string>();
        static readonly Dictionary<string, string> equipped = new Dictionary<string, string>(); // key -> item id
        static readonly Dictionary<string, int> xp = new Dictionary<string, int>();              // rifle id -> xp

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
            LeveledUp = null;
        }

        public static int Credits
        {
            get
            {
                Load();
                return credits;
            }
        }

        // ------------------------------------------------------------------ weapon levels

        /// <summary>Total XP needed to reach a level. Level 2 is 50, level 5 is 800, level 12 is 6,050.</summary>
        public static int XpForLevel(int level)
        {
            int steps = Mathf.Max(0, level - 1);
            return 50 * steps * steps;
        }

        public static int WeaponXp(ShopItem rifle)
        {
            Load();
            return rifle != null && xp.TryGetValue(rifle.id, out int value) ? value : 0;
        }

        public static int WeaponLevel(ShopItem rifle)
        {
            int level = 1 + Mathf.FloorToInt(Mathf.Sqrt(WeaponXp(rifle) / 50f));
            return Mathf.Clamp(level, 1, MaxWeaponLevel);
        }

        /// <summary>How far through the current level the rifle is, from 0 to 1.</summary>
        public static float LevelProgress(ShopItem rifle)
        {
            int level = WeaponLevel(rifle);
            if (level >= MaxWeaponLevel) return 1f;
            float from = XpForLevel(level);
            float to = XpForLevel(level + 1);
            return Mathf.Clamp01((WeaponXp(rifle) - from) / (to - from));
        }

        /// <summary>Gives XP to the equipped rifle. Called for every kill.</summary>
        public static void AddWeaponXp(int amount)
        {
            ShopItem rifle = Equipped(ItemSlot.Rifle);
            if (rifle == null || amount <= 0) return;

            int before = WeaponLevel(rifle);
            int total = WeaponXp(rifle) + amount;
            xp[rifle.id] = total;
            PlayerPrefs.SetInt("Trickshot.Xp." + rifle.id, total); // written to disk at the next Save

            int after = WeaponLevel(rifle);
            if (after > before)
            {
                PlayerPrefs.Save();
                LeveledUp?.Invoke(rifle, after);
                Changed?.Invoke();
            }
        }

        // ------------------------------------------------------------------ ownership and loadout

        public static bool Owns(ShopItem item)
        {
            Load();
            return item.price <= 0 || owned.Contains(item.id);
        }

        /// <summary>Owned, and (for rifle skins) the equipped rifle is a high enough level.</summary>
        public static bool Unlocked(ShopItem item)
        {
            if (!Owns(item)) return false;
            return item.slot != ItemSlot.RifleFinish || WeaponLevel(Equipped(ItemSlot.Rifle)) >= item.level;
        }

        /// <summary>The item worn in a slot. Falls back to the free one. The rifle skin is per rifle.</summary>
        public static ShopItem Equipped(ItemSlot slot)
        {
            Load();
            equipped.TryGetValue(Key(slot), out string id);

            ShopItem free = null;
            foreach (ShopItem item in Catalog)
            {
                if (item.slot != slot) continue;
                if (item.id == id && Unlocked(item)) return item;
                if (free == null && item.price <= 0 && item.level <= 1) free = item;
            }
            return free;
        }

        public static void Equip(ShopItem item)
        {
            if (!Unlocked(item)) return;
            equipped[Key(item.slot)] = item.id;
            Save();
        }

        /// <summary>Buys an item if there are enough credits. Returns true if it is now owned.</summary>
        public static bool Buy(ShopItem item)
        {
            if (Owns(item)) return true;
            if (credits < item.price) return false;
            credits -= item.price;
            owned.Add(item.id);
            Save();
            return true;
        }

        public static void AddCredits(int amount)
        {
            Load();
            if (amount <= 0) return;
            credits += amount;
            Save();
        }

        // Where a slot's choice is stored. Each rifle keeps its own skin.
        static string Key(ItemSlot slot)
        {
            if (slot != ItemSlot.RifleFinish) return slot.ToString();
            ShopItem rifle = Equipped(ItemSlot.Rifle);
            return "RifleFinish." + (rifle != null ? rifle.id : "");
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;

            credits = PlayerPrefs.GetInt("Trickshot.Credits", StartingCredits);

            owned.Clear();
            foreach (string id in PlayerPrefs.GetString("Trickshot.Owned", "").Split(','))
            {
                if (id.Length > 0) owned.Add(id);
            }

            equipped.Clear();
            xp.Clear();
            var keys = new List<string> { ItemSlot.Rifle.ToString(), ItemSlot.Outfit.ToString(), ItemSlot.Hat.ToString() };
            foreach (ShopItem item in Catalog)
            {
                if (item.slot != ItemSlot.Rifle) continue;
                keys.Add("RifleFinish." + item.id);
                xp[item.id] = PlayerPrefs.GetInt("Trickshot.Xp." + item.id, 0);
            }
            foreach (string key in keys)
            {
                string id = PlayerPrefs.GetString("Trickshot.Equipped." + key, "");
                if (id.Length > 0) equipped[key] = id;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetInt("Trickshot.Credits", credits);
            PlayerPrefs.SetString("Trickshot.Owned", string.Join(",", owned));
            foreach (KeyValuePair<string, string> pair in equipped)
                PlayerPrefs.SetString("Trickshot.Equipped." + pair.Key, pair.Value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
