using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FoundationsUnchained.Patches
{
    /// <summary>
    /// Removes obstacle/collision checks for foundation placement when free placement
    /// mode is active, allowing foundations to be placed next to existing structures
    /// without being blocked by their colliders.
    /// </summary>
    [HarmonyPatch(typeof(Builder), nameof(Builder.GetObstacles))]
    internal static class NoCollisionPatch
    {
        [HarmonyPostfix]
        static void Postfix(List<GameObject> results)
        {
            if (!Plugin.freeSnapEnabled)
                return;

            if (Builder.lastTechType != TechType.BaseFoundation)
                return;

            results.Clear();
        }
    }

    /// <summary>
    /// Also bypass CheckSpace for foundations in free placement mode.
    /// </summary>
    [HarmonyPatch(typeof(Builder), nameof(Builder.CheckSpace),
        new[] { typeof(Vector3), typeof(Quaternion), typeof(List<OrientedBounds>), typeof(int), typeof(Collider) })]
    internal static class NoCheckSpacePatch
    {
        [HarmonyPostfix]
        static void Postfix(ref bool __result)
        {
            if (!Plugin.freeSnapEnabled)
                return;

            if (Builder.lastTechType != TechType.BaseFoundation)
                return;

            __result = true;
        }
    }
}
