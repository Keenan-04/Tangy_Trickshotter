using System.Collections.Generic;
using UnityEngine;

namespace Trickshot
{
    public enum ItemSlot
    {
        RifleFinish,
        Outfit
    }

    /// <summary>One thing that can be owned and equipped. For now an item is just a colour.</summary>
    public class ShopItem
    {
        public string id;
        public string name;
        public ItemSlot slot;
        public Color color;
        public int price; // 0 = everyone owns it

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
    /// The catalogue is a placeholder list of colours; replace it with real items later.
    /// </summary>
    public static class Profile
    {
        public const int StartingCredits = 500;

        public static readonly ShopItem[] Catalog =
        {
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
