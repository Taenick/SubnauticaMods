using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DeletePlugin;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private ConfigEntry<KeyCode> deleteKey;
    private ConfigEntry<float> areaDeleteRadius;

    internal static HashSet<string> deletedKeys = new HashSet<string>();
    internal static bool deletedKeysLoaded = false;

    private static readonly Type[] blockedComponents = new Type[]
    {
        typeof(Player),
        typeof(EscapePod),
    };

    private Harmony harmony;
    private bool initialScanDone = false;

    private void Awake()
    {
        Logger = base.Logger;

        deleteKey = Config.Bind(
            "General",
            "DeleteKey",
            KeyCode.Delete,
            "Key to press to delete the object you are looking at."
        );

        areaDeleteRadius = Config.Bind(
            "General",
            "AreaDeleteRadius",
            5f,
            "Radius in meters for area delete (Alt+Delete)."
        );

        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        DevConsole.RegisterConsoleCommand("delete", OnConsoleCommand_delete);
        SaveLoadManager.notificationSaveInProgress += OnSaveInProgress;

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnDestroy()
    {
        SaveLoadManager.notificationSaveInProgress -= OnSaveInProgress;
        harmony?.UnpatchSelf();
    }

    private void Update()
    {
        if (Player.main == null)
            return;

        EnsureDeletedKeysLoaded();

        if (!initialScanDone && deletedKeys.Count > 0)
        {
            ScanAndDestroyDeletedObjects();
            initialScanDone = true;
        }

        if (uGUI.main != null && uGUI.main.userInput.selected)
            return;

        if (Input.GetKeyDown(deleteKey.Value))
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                TryAreaDelete();
            }
            else
            {
                TryDeleteTarget();
            }
        }
    }

    private static void OnConsoleCommand_delete(NotificationCenter.Notification n)
    {
        TryDeleteTarget();
    }

    private static void DropStorageContents(GameObject obj)
    {
        var storages = obj.GetComponentsInChildren<StorageContainer>();
        foreach (var storage in storages)
        {
            if (storage == null || storage.container == null || storage.container.count == 0)
                continue;

            // Collect items first to avoid modifying collection while iterating
            var items = new List<Pickupable>();
            foreach (InventoryItem item in storage.container)
            {
                if (item?.item != null)
                    items.Add(item.item);
            }

            // Drop 3m in front of camera (matches game's default drop behavior)
            Vector3 dropPos = MainCamera.camera.transform.position + MainCamera.camera.transform.forward * 3f;
            foreach (var pickupable in items)
            {
                storage.container.RemoveItem(pickupable, true);
                if (!Inventory.main.Pickup(pickupable))
                {
                    pickupable.Drop(dropPos, Vector3.zero, false);
                }
            }

            if (items.Count > 0)
                Logger.LogInfo($"Recovered {items.Count} items from storage.");
        }
    }

    private static void RefundIngredients(TechType techType, Vector3 dropPos)
    {
        if (techType == TechType.None)
            return;

        ReadOnlyCollection<Ingredient> ingredients = TechData.GetIngredients(techType);
        if (ingredients == null)
            return;

        foreach (Ingredient ingredient in ingredients)
        {
            CraftData.AddToInventory(ingredient.techType, ingredient.amount, noMessage: false, spawnIfCantAdd: true);
        }
        Logger.LogInfo($"Refunded ingredients for {techType}");
    }

    internal static string MakeKey(string classId, Vector3 pos)
    {
        return $"{classId}@{pos.x:F1},{pos.y:F1},{pos.z:F1}";
    }

    private static bool TryDeleteTarget()
    {
        Targeting.GetTarget(Player.main.gameObject, 100f, out GameObject target, out float _);

        if (target == null)
        {
            ErrorMessage.AddMessage("Nothing to delete.");
            return false;
        }

        // Find BaseDeconstructable — same lookup as the game's BuilderTool
        BaseDeconstructable baseDecon = target.GetComponentInParent<BaseDeconstructable>();
        if (baseDecon == null)
        {
            var explicitFace = target.GetComponentInParent<BaseExplicitFace>();
            if (explicitFace != null)
                baseDecon = explicitFace.parent;
        }

        // Check for Constructable items (wall lockers, signs, etc.) inside a base
        if (baseDecon == null)
        {
            Constructable constructable = target.GetComponentInParent<Constructable>();
            if (constructable != null)
            {
                DropStorageContents(constructable.gameObject);
                RefundIngredients(constructable.techType, constructable.transform.position);
                UnityEngine.Object.Destroy(constructable.gameObject);
                ErrorMessage.AddMessage($"Deleted: {constructable.techType}");
                Logger.LogInfo($"Deleted constructable: {constructable.techType}");
                return true;
            }

            // If we hit part of a Base but no deconstructable, search the cell
            Base hitBase = target.GetComponentInParent<Base>();
            if (hitBase != null)
            {
                var cellDecon = target.GetComponentInParent<Transform>()
                    ?.parent?.GetComponentInChildren<BaseDeconstructable>();
                if (cellDecon != null)
                {
                    baseDecon = cellDecon;
                }
                else
                {
                    ErrorMessage.AddMessage("Cannot delete: aim at the base piece directly.");
                    return false;
                }
            }
        }

        if (baseDecon != null)
        {
            return TryDeconstructBasePiece(baseDecon);
        }

        // Handle as a world object
        // For Cyclops: hitting interior objects should target the whole sub
        SubRoot targetSub = target.GetComponentInParent<SubRoot>();
        GameObject root;
        if (targetSub != null && targetSub.isCyclops)
        {
            root = targetSub.gameObject;
        }
        else
        {
            PrefabIdentifier prefabId = target.GetComponentInParent<PrefabIdentifier>();
            if (prefabId != null)
            {
                root = prefabId.gameObject;
            }
            else
            {
                UniqueIdentifier uniqueId = target.GetComponentInParent<UniqueIdentifier>();
                if (uniqueId != null)
                {
                    root = uniqueId.gameObject;
                }
                else
                {
                    LargeWorldEntity lwe = target.GetComponentInParent<LargeWorldEntity>();
                    if (lwe != null)
                    {
                        root = lwe.gameObject;
                    }
                    else if (target.GetComponentInParent<Renderer>() != null)
                    {
                        root = target.GetComponentInParent<Renderer>().gameObject;
                    }
                    else
                    {
                        ErrorMessage.AddMessage("Cannot delete: not a deletable object.");
                        return false;
                    }
                }
            }

            // Block terrain
            if (root.layer == LayerID.TerrainCollider ||
                root.GetComponent<Voxeland>() != null)
            {
                ErrorMessage.AddMessage("Cannot delete: terrain is protected.");
                return false;
            }
        }

        // Block protected objects
        foreach (Type blocked in blockedComponents)
        {
            if (root.GetComponent(blocked) != null)
            {
                ErrorMessage.AddMessage($"Cannot delete: {root.name} (protected)");
                return false;
            }
        }

        string objectName = root.name;

        // Block deletion of the vehicle you're currently piloting
        Vehicle vehicle = root.GetComponent<Vehicle>();
        SubRoot subRoot = root.GetComponent<SubRoot>();
        if ((vehicle != null && vehicle.GetPilotingMode()) ||
            (subRoot != null && Player.main.GetCurrentSub() == subRoot))
        {
            ErrorMessage.AddMessage("Cannot delete: you are piloting this vehicle!");
            return false;
        }

        // Use LiveMixin.Kill() for objects with health (vehicles, creatures, etc.)
        LiveMixin liveMixin = root.GetComponent<LiveMixin>();
        if (liveMixin != null)
        {
            liveMixin.Kill();
            if (root != null)
                UnityEngine.Object.Destroy(root);
            ErrorMessage.AddMessage($"Destroyed: {objectName}");
            Logger.LogInfo($"Destroyed: {objectName}");
            return true;
        }

        // For static world objects, use Destroy + track for persistence
        UniqueIdentifier uid = root.GetComponent<UniqueIdentifier>();
        if (uid != null && !string.IsNullOrEmpty(uid.ClassId))
        {
            string key = MakeKey(uid.ClassId, root.transform.position);
            deletedKeys.Add(key);
        }

        UnityEngine.Object.Destroy(root);

        ErrorMessage.AddMessage($"Deleted: {objectName}");
        Logger.LogInfo($"Deleted: {objectName}");
        return true;
    }

    private void TryAreaDelete()
    {
        Transform cam = MainCamera.camera.transform;
        float radius = areaDeleteRadius.Value;

        Vector3 center;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 100f))
        {
            center = hit.point;
        }
        else
        {
            center = cam.position + cam.forward * 10f;
        }

        var allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        int destroyed = 0;

        foreach (var renderer in allRenderers)
        {
            if (renderer == null || renderer.gameObject == null)
                continue;

            if (Vector3.Distance(renderer.transform.position, center) > radius)
                continue;

            GameObject obj = renderer.gameObject;

            PrefabIdentifier prefabId = obj.GetComponentInParent<PrefabIdentifier>();
            UniqueIdentifier uniqueId = obj.GetComponentInParent<UniqueIdentifier>();
            LargeWorldEntity lwe = obj.GetComponentInParent<LargeWorldEntity>();
            GameObject root = prefabId?.gameObject ?? uniqueId?.gameObject ?? lwe?.gameObject ?? obj;

            // Skip protected objects
            if (root.GetComponent<Player>() != null) continue;
            if (root.GetComponent<EscapePod>() != null) continue;
            if (root.GetComponentInParent<Base>() != null) continue;
            if (root.GetComponentInParent<SubRoot>() != null) continue;
            if (root.GetComponentInParent<Vehicle>() != null) continue;
            if (root.layer == LayerID.TerrainCollider) continue;
            if (root.GetComponent<Voxeland>() != null) continue;

            // Skip terrain chunks
            string rootName = root.name;
            if (rootName.StartsWith("Chunk") ||
                rootName.StartsWith("%.%.%") ||
                rootName.StartsWith("%.%") ||
                root.GetComponentInParent<VoxelandChunk>() != null)
                continue;

            if (root == null) continue;

            UnityEngine.Object.Destroy(root);
            destroyed++;
        }

        ErrorMessage.AddMessage($"Area delete: removed {destroyed} objects within {radius}m.");
        Logger.LogInfo($"Area delete at {center}: removed {destroyed} objects.");
    }

    private static bool TryDeconstructBasePiece(BaseDeconstructable baseDecon)
    {
        Base parentBase = baseDecon.GetComponentInParent<Base>();
        if (parentBase == null)
        {
            ErrorMessage.AddMessage("Cannot delete: no parent base found.");
            return false;
        }

        string pieceName = baseDecon.Name;

        // Destroy Constructable items (wall lockers, etc.) attached to this piece
        var cellWorldMin = parentBase.GridToWorld(baseDecon.bounds.mins);
        var cellWorldMax = parentBase.GridToWorld(baseDecon.bounds.maxs + new Int3(1, 1, 1));
        var allConstructables = parentBase.GetComponentsInChildren<Constructable>();
        foreach (var c in allConstructables)
        {
            if (c == null || c.gameObject == null)
                continue;
            Vector3 pos = c.transform.position;
            if (pos.x >= Mathf.Min(cellWorldMin.x, cellWorldMax.x) - 1f &&
                pos.x <= Mathf.Max(cellWorldMin.x, cellWorldMax.x) + 1f &&
                pos.y >= Mathf.Min(cellWorldMin.y, cellWorldMax.y) - 1f &&
                pos.y <= Mathf.Max(cellWorldMin.y, cellWorldMax.y) + 1f &&
                pos.z >= Mathf.Min(cellWorldMin.z, cellWorldMax.z) - 1f &&
                pos.z <= Mathf.Max(cellWorldMin.z, cellWorldMax.z) + 1f)
            {
                DropStorageContents(c.gameObject);
                RefundIngredients(c.techType, c.transform.position);
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        // Refund recipe ingredients to the player's inventory
        var recipeField = AccessTools.Field(typeof(BaseDeconstructable), "recipe");
        if (recipeField != null)
        {
            TechType recipe = (TechType)recipeField.GetValue(baseDecon);
            RefundIngredients(recipe, baseDecon.transform.position);
        }

        // Clear the cell or face from the base grid
        if (baseDecon.face.HasValue && baseDecon.faceType != Base.FaceType.None)
        {
            parentBase.ClearFace(baseDecon.face.Value, baseDecon.faceType);
        }
        else
        {
            parentBase.ClearCell(baseDecon.bounds.mins);
        }

        if (parentBase.IsEmpty())
        {
            parentBase.OnPreDestroy();
            UnityEngine.Object.Destroy(parentBase.gameObject);
        }
        else
        {
            // Mark neighboring cells as touched so they rebuild with support pillars
            var touchCell = AccessTools.Method(typeof(Base), "TouchCell");
            if (touchCell != null)
            {
                foreach (Int3 cell in baseDecon.bounds)
                {
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(0, 1, 0) });
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(0, -1, 0) });
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(1, 0, 0) });
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(-1, 0, 0) });
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(0, 0, 1) });
                    touchCell.Invoke(parentBase, new object[] { cell + new Int3(0, 0, -1) });
                }
            }

            parentBase.FixRoomFloors();
            parentBase.FixCorridorLinks();
            parentBase.RebuildGeometry();
        }

        ErrorMessage.AddMessage($"Deleted: {pieceName}");
        Logger.LogInfo($"Deleted base piece: {pieceName}");
        return true;
    }

    internal void ResetScan()
    {
        initialScanDone = false;
    }

    private static void ScanAndDestroyDeletedObjects()
    {
        var allIds = UnityEngine.Object.FindObjectsOfType<UniqueIdentifier>();
        int destroyed = 0;

        foreach (var uid in allIds)
        {
            if (uid == null || uid.gameObject == null)
                continue;

            string classId = uid.ClassId;
            if (string.IsNullOrEmpty(classId))
                continue;

            string key = MakeKey(classId, uid.transform.position);
            if (deletedKeys.Contains(key))
            {
                UnityEngine.Object.Destroy(uid.gameObject);
                destroyed++;
            }
        }

        if (destroyed > 0)
            Logger.LogInfo($"Initial scan: suppressed {destroyed} deleted objects.");
    }

    internal static string GetSlotSavePath()
    {
        if (SaveLoadManager.main == null)
            return null;

        string slot = SaveLoadManager.main.GetCurrentSlot();
        if (string.IsNullOrEmpty(slot))
            return null;

        string saveRoot = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "SNAppData",
            "SavedGames"
        );
        return Path.Combine(saveRoot, slot);
    }

    private static void OnSaveInProgress(bool saving)
    {
        if (!saving)
            return;

        string slotPath = GetSlotSavePath();
        if (string.IsNullOrEmpty(slotPath))
            return;

        SaveDeletedKeys(slotPath);
    }

    internal static void EnsureDeletedKeysLoaded()
    {
        if (deletedKeysLoaded)
            return;

        string slotPath = GetSlotSavePath();
        if (string.IsNullOrEmpty(slotPath))
            return;

        deletedKeysLoaded = true;
        deletedKeys.Clear();

        string filePath = Path.Combine(slotPath, "deleted-objects.json");
        if (!File.Exists(filePath))
            return;

        try
        {
            string json = File.ReadAllText(filePath);
            var data = JsonUtility.FromJson<DeletedObjectsData>(json);
            if (data?.keys != null)
            {
                foreach (string key in data.keys)
                    deletedKeys.Add(key);
            }
            Logger.LogInfo($"Loaded {deletedKeys.Count} deleted object keys.");
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to load deleted objects: {e.Message}");
        }
    }

    private static void SaveDeletedKeys(string slotPath)
    {
        string filePath = Path.Combine(slotPath, "deleted-objects.json");

        if (deletedKeys.Count == 0)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            return;
        }

        try
        {
            var data = new DeletedObjectsData();
            data.keys = new List<string>(deletedKeys);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json);
            Logger.LogInfo($"Saved {deletedKeys.Count} deleted object keys.");
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to save deleted objects: {e.Message}");
        }
    }

    [Serializable]
    internal class DeletedObjectsData
    {
        public List<string> keys = new List<string>();
    }
}

[HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.SetCurrentSlot))]
internal static class SaveLoadPatch
{
    [HarmonyPostfix]
    static void Postfix()
    {
        Plugin.deletedKeysLoaded = false;
        Plugin.deletedKeys.Clear();
        var plugin = UnityEngine.Object.FindObjectOfType<Plugin>();
        if (plugin != null)
            plugin.ResetScan();
    }
}

[HarmonyPatch(typeof(LargeWorldEntity), "Awake")]
internal static class LargeWorldEntityPatch
{
    [HarmonyPostfix]
    static void Postfix(LargeWorldEntity __instance)
    {
        Plugin.EnsureDeletedKeysLoaded();

        if (Plugin.deletedKeys.Count == 0)
            return;

        UniqueIdentifier uid = __instance.GetComponent<UniqueIdentifier>();
        if (uid == null)
            return;

        string classId = uid.ClassId;
        if (string.IsNullOrEmpty(classId))
            return;

        string key = Plugin.MakeKey(classId, __instance.transform.position);
        if (Plugin.deletedKeys.Contains(key))
        {
            UnityEngine.Object.Destroy(__instance.gameObject);
        }
    }
}
