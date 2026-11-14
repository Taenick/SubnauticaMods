using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PrawnStorageModuleFix.Patches;

namespace PrawnStorageModuleFix;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.mrpurple6411.BetterVehicleStorage", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmony;

    private void Awake()
    {
        Logger = base.Logger;
        ExosuitStorageState.Init();
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
