using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PrawnStorageModuleFix.Patches;

internal static class ExosuitStorageState
{
    public static readonly string[] moduleSlots = { "ExosuitModule1", "ExosuitModule2", "ExosuitModule3", "ExosuitModule4" };

    public static Exosuit activeExosuit;

    private const int baseSlots = 16;
    private const int vanillaModuleSlots = 16;
    private const int moduleWidth = 8;

    // BetterVehicleStorage integration — populated via reflection at startup
    private static Func<TechType, bool> isStorageModule;
    private static Dictionary<TechType, int> tierCapacities;

    public static void Init()
    {
        tierCapacities = new Dictionary<TechType, int>();

        try
        {
            var assembly = Assembly.Load("BetterVehicleStorage");
            var mgrType = assembly.GetType("BetterVehicleStorage.Managers.StorageModuleMgr");
            if (mgrType == null) return;

            // Get IsStorageModule method
            var isStorageMethod = mgrType.GetMethod("IsStorageModule", BindingFlags.Public | BindingFlags.Static);
            if (isStorageMethod != null)
                isStorageModule = (Func<TechType, bool>)Delegate.CreateDelegate(typeof(Func<TechType, bool>), isStorageMethod);

            // Read Modules dictionary to get tier capacities
            var modulesField = mgrType.GetField("Modules", BindingFlags.Public | BindingFlags.Static);
            if (modulesField == null) return;

            var modules = modulesField.GetValue(null);
            if (modules == null) return;

            // Modules is IDictionary<TechType, StorageModule>
            // StorageModule has StorageWidth and StorageHeight fields
            foreach (var key in (IEnumerable<TechType>)modules.GetType().GetProperty("Keys").GetValue(modules))
            {
                var entry = modules.GetType().GetProperty("Item").GetValue(modules, new object[] { key });
                var w = (int)entry.GetType().GetField("StorageWidth").GetValue(entry);
                var h = (int)entry.GetType().GetField("StorageHeight").GetValue(entry);
                tierCapacities[key] = w * h;
            }

            Plugin.Logger.LogDebug($"[ExosuitStorage] BetterVehicleStorage detected, loaded {tierCapacities.Count} tiered modules");
        }
        catch (Exception)
        {
            // BetterVehicleStorage not installed — vanilla only
        }
    }

    public static int GetModuleCapacity(TechType techType)
    {
        if (techType == TechType.VehicleStorageModule)
            return vanillaModuleSlots;

        if (tierCapacities != null && tierCapacities.TryGetValue(techType, out int capacity))
            return capacity;

        // Unknown module — check if BetterVehicleStorage recognizes it
        if (isStorageModule != null && isStorageModule(techType))
            return vanillaModuleSlots;

        return 0;
    }

    public static void GetExpectedSize(Exosuit exosuit, out int width, out int height)
    {
        int totalSlots = baseSlots;

        foreach (string slot in moduleSlots)
        {
            TechType techType = exosuit.modules.GetTechTypeInSlot(slot);
            totalSlots += GetModuleCapacity(techType);
        }

        width = moduleWidth;
        height = totalSlots / moduleWidth;  // 8x2 = 16 base, 8-wide grid always
    }
}

[HarmonyPatch(typeof(Exosuit), "Start")]
internal static class ExosuitStorageStartPatch
{
    [HarmonyPrefix]
    static void Prefix(Exosuit __instance)
    {
    }
}

[HarmonyPatch(typeof(Exosuit), "Update")]
internal static class ExosuitStorageUpdatePatch
{
    [HarmonyPostfix]
    static void Postfix(Exosuit __instance)
    {
        ExosuitStorageState.GetExpectedSize(__instance, out int expectedWidth, out int expectedHeight);
        var container = __instance.storageContainer;

        ExosuitStorageState.activeExosuit = __instance;

        if (container.container != null &&
            (container.container.sizeX != expectedWidth || container.container.sizeY != expectedHeight))
        {
            container.Resize(expectedWidth, expectedHeight);
        }
    }
}
