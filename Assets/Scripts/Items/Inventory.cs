using System;
using UnityEngine;

/// <summary>
/// Oyuncunun cantasi: 4 slot. Ayni esya ust uste biner (Altin 9999'a kadar tek slotta).
/// Olunce (sahne yeniden yuklenince) kaybolmaz; ana menuden "BASLA" denince sifirlanir.
/// Kullanim: Inventory.Add(ItemDefinition.Gold, 1), Inventory.Count(ItemDefinition.Gold)
/// Ekrandaki slot bari: InventoryBar
/// </summary>
public static class Inventory
{
    public const int SlotCount = 4;

    public struct Slot
    {
        public ItemDefinition item;
        public int count;
        public bool IsEmpty => item == null || count <= 0;
    }

    private static readonly Slot[] slots = new Slot[SlotCount];

    /// <summary>Herhangi bir slot degisince (slot indeksi).</summary>
    public static event Action<int> Changed;
    /// <summary>Esya alininca (esya, alinan miktar, slot indeksi).</summary>
    public static event Action<ItemDefinition, int, int> Added;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        Added = null;
        for (int i = 0; i < SlotCount; i++) slots[i] = default;
    }

    public static Slot Get(int index) => index >= 0 && index < SlotCount ? slots[index] : default;

    public static int Count(ItemDefinition item)
    {
        int n = 0;
        foreach (var s in slots) if (s.item == item) n += s.count;
        return n;
    }

    /// <summary>Esyayi ekler. Sigmayan miktari dondurur (0 = hepsi alindi).</summary>
    public static int Add(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return amount;
        int left = amount;
        // once ayni esyanin oldugu slotlar, sonra bos slotlar
        for (int pass = 0; pass < 2 && left > 0; pass++)
            for (int i = 0; i < SlotCount && left > 0; i++)
            {
                var s = slots[i];
                bool match = pass == 0 ? s.item == item && s.count < item.maxStack : s.IsEmpty;
                if (!match) continue;
                if (s.IsEmpty) { s.item = item; s.count = 0; }
                int put = Mathf.Min(left, item.maxStack - s.count);
                s.count += put;
                left -= put;
                slots[i] = s;
                Changed?.Invoke(i);
                Added?.Invoke(item, put, i);
            }
        return left;
    }

    /// <summary>Esyadan miktar kadar harcar. Yetmezse hic harcamaz ve false doner.</summary>
    public static bool Remove(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return true;
        if (Count(item) < amount) return false;
        for (int i = SlotCount - 1; i >= 0 && amount > 0; i--)
        {
            var s = slots[i];
            if (s.item != item) continue;
            int take = Mathf.Min(amount, s.count);
            s.count -= take;
            amount -= take;
            if (s.count <= 0) s = default;
            slots[i] = s;
            Changed?.Invoke(i);
        }
        return true;
    }

    public static bool HasRoomFor(ItemDefinition item)
    {
        foreach (var s in slots)
            if (s.IsEmpty || (s.item == item && s.count < item.maxStack)) return true;
        return false;
    }

    public static void Clear()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i].IsEmpty) continue;
            slots[i] = default;
            Changed?.Invoke(i);
        }
    }
}
