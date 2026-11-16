using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum ItemSlot
    {
        Rifle,
        RifleFinish,
        Outfit
    }

    /// <summary>One thing that can be owned and equipped: a rifle, or a colour for the rifle or the outfit.</summary>
    public class ShopItem
    {
        public string id;
        public string name;
        public ItemSlot slot;
        public Color color;
        public int price; // 0 = everyone owns it
        public WeaponStats weapon; // only for rifles

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
    /// TEMPLATE. The player's credits, owned items and loadout, saved in PlayerPrefs.
    /// The catalogue holds the five rifles (models in Resources/Rifles) plus placeholder colours
    /// for rifle finishes and outfits. Prices and rifle stats are first guesses.
    /// </summary>
    public static class Profile
    {
        public const int StartingCredits = 500;

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

            new ShopItem("finish.black", "Matte Black", ItemSlot.RifleFinish, new Color(0.12f, 0.12f, 0.14f), 0),
            new ShopItem("finish.tangy", "Tangy Orange", ItemSlot.RifleFinish, new Color(1f, 0.55f, 0.05f), 300),
            new ShopItem("finish.arctic", "Arctic White", ItemSlot.RifleFinish, new Color(0.9f, 0.93f, 0.96f), 400),
            new ShopItem("finish.lime", "Lime Shock", ItemSlot.RifleFinish, new Color(0.55f, 0.95f, 0.15f), 600),
            new ShopItem("finish.royal", "Royal Purple", ItemSlot.RifleFinish, new Color(0.5f, 0.25f, 0.85f), 800),

            new ShopItem("outfit.recruit", "Recruit Grey", ItemSlot.Outfit, new Color(0.42f, 0.45f, 0.5f), 0),
            new ShopItem("outfit.desert", "Desert Tan", ItemSlot.Outfit, new Color(0.76f, 0.64f, 0.44f), 250),
            new ShopItem("outfit.night", "Night Ops", ItemSlot.Outfit, new Color(0.1f, 0.13f, 0.22f), 500),
            new ShopItem("outfit.crimson", "Crimson", ItemSlot.Outfit, new Color(0.7f, 0.1f, 0.14f), 750),
        };

        /// <summary>Raised whenever credits, ownership or the loadout change.</summary>
        public static event System.Action Changed;

        static bool loaded;
        static int credits;
        static readonly HashSet<string> owned = new HashSet<string>();
        static readonly Dictionary<ItemSlot, string> equipped = new Dictionary<ItemSlot, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        public static int Credits
        {
            get
            {
                Load();
                return credits;
            }
        }

        public static bool Owns(ShopItem item)
        {
            Load();
            return item.price <= 0 || owned.Contains(item.id);
        }

        /// <summary>The item worn in a slot. Falls back to the free one.</summary>
        public static ShopItem Equipped(ItemSlot slot)
        {
            Load();
            ShopItem free = null;
            equipped.TryGetValue(slot, out string id);
            foreach (ShopItem item in Catalog)
            {
                if (item.slot != slot) continue;
                if (item.id == id && Owns(item)) return item;
                if (free == null && item.price <= 0) free = item;
            }
            return free;
        }

        public static void Equip(ShopItem item)
        {
            if (!Owns(item)) return;
            equipped[item.slot] = item.id;
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
            foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
            {
                string id = PlayerPrefs.GetString("Trickshot.Equipped." + slot, "");
                if (id.Length > 0) equipped[slot] = id;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetInt("Trickshot.Credits", credits);
            PlayerPrefs.SetString("Trickshot.Owned", string.Join(",", owned));
            foreach (KeyValuePair<ItemSlot, string> pair in equipped)
                PlayerPrefs.SetString("Trickshot.Equipped." + pair.Key, pair.Value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
