using HarmonyLib;
using UnityEngine;

namespace FoundationsUnchained.Patches
{
    [HarmonyPatch(typeof(BaseGhost), "FindBase")]
    internal static class FindBasePatch
    {
        internal static bool hasNearbyBase = false;
        internal static Quaternion nearbyBaseRotation = Quaternion.identity;
        internal static Base nearbyBase = null;

        [HarmonyPrefix]
        static bool Prefix(Transform camera, ref float searchDistance, ref Base __result)
        {
            if (Builder.lastTechType != TechType.BaseFoundation)
                return true;

            float configRange = Plugin.snapSearchRange.Value;
            if (configRange > searchDistance)
                searchDistance = configRange;

            if (!Plugin.freeSnapEnabled)
            {
                hasNearbyBase = false;
                nearbyBase = null;
                return true;
            }

            hasNearbyBase = false;
            nearbyBase = null;

            LayerMask mask = ~((1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("Trigger")));

            if (Physics.SphereCast(new Ray(camera.position, camera.forward), 0.5f, out var hit, searchDistance, mask.value))
            {
                Base b = hit.collider.GetComponentInParent<Base>();
                if (b != null && b.GetComponent<BaseGhost>() == null)
                {
                    hasNearbyBase = true;
                    nearbyBase = b;
                    nearbyBaseRotation = b.transform.rotation;
                    __result = null;
                    return false;
                }
            }

            int count = UWE.Utils.OverlapSphereIntoSharedBuffer(
                camera.position + camera.forward * searchDistance * 0.5f,
                searchDistance * 0.5f, mask.value);

            for (int i = 0; i < count; i++)
            {
                Base b = UWE.Utils.sharedColliderBuffer[i].GetComponentInParent<Base>();
                if (b != null && b.GetComponent<BaseGhost>() == null)
                {
                    hasNearbyBase = true;
                    nearbyBase = b;
                    nearbyBaseRotation = b.transform.rotation;
                    break;
                }
            }

            __result = null;
            return false;
        }
    }
}
