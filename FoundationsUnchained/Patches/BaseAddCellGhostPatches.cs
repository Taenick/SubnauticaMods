using HarmonyLib;
using UnityEngine;

namespace FoundationsUnchained.Patches
{
    /// <summary>
    /// Always disables the Snap() alignment for foundations.
    /// </summary>
    [HarmonyPatch(typeof(BaseAddCellGhost), "Snap")]
    internal static class FreeSnapPatch
    {
        [HarmonyPrefix]
        static bool Prefix(Int3 cell, Base.CellType cellType, ref Int3 __result)
        {
            if (cellType != Base.CellType.Foundation)
                return true;

            __result = cell;
            return false;
        }
    }

    /// <summary>
    /// Single consolidated postfix for all foundation placement logic:
    /// 1. Relaxed validation (allow placement below observatories etc)
    /// 2. Grid nudge when attached / locked position when free-placing
    /// 3. Rotation
    /// All in one patch to avoid ordering issues between multiple postfixes.
    /// </summary>
    [HarmonyPatch(typeof(BaseAddCellGhost), nameof(BaseAddCellGhost.UpdatePlacement))]
    internal static class FoundationPlacementPatch
    {
        [HarmonyPostfix]
        static void Postfix(BaseAddCellGhost __instance, ref bool __result,
            ref bool positionFound, ConstructableBase ghostModelParentConstructableBase)
        {
            if (__instance.cellTypes == null || __instance.cellTypes.Count == 0)
                return;
            if (__instance.cellTypes[0] != Base.CellType.Foundation)
                return;

            Base targetBase = __instance.TargetBase;
            Transform t = ghostModelParentConstructableBase.transform;

            // === STEP 1: Relaxed validation ===
            if (!__result)
            {
                if (targetBase != null)
                {
                    Int3 offset = __instance.targetOffset;
                    Int3 size = Base.CellSize[(uint)Base.CellType.Foundation];
                    Int3 maxs = offset + size - 1;
                    bool cellsEmpty = true;

                    foreach (Int3 pos in new Int3.Bounds(offset, maxs))
                    {
                        if (targetBase.GetCell(pos) != Base.CellType.Empty || targetBase.IsCellUnderConstruction(pos))
                        {
                            cellsEmpty = false;
                            break;
                        }
                    }

                    if (cellsEmpty)
                    {
                        __result = true;
                        positionFound = true;
                    }
                }
                else if (Plugin.freeSnapEnabled)
                {
                    __result = true;
                    positionFound = true;
                }
            }

            // === STEP 2: Position control ===
            if (targetBase != null)
            {
                // Attached to base: grid cell nudge
                bool hasNudge = Plugin.nudgeX != 0 || Plugin.nudgeZ != 0;
                bool hasVertical = Plugin.enableVerticalSnap.Value && Plugin.verticalOffset != 0;

                if (hasNudge || hasVertical)
                {
                    Int3 gridNudge = Int3.zero;

                    if (hasNudge)
                    {
                        Transform cam = MainCamera.camera.transform;
                        Vector3 camForward = cam.forward;
                        camForward.y = 0f;
                        camForward.Normalize();

                        Vector3 localFwd = targetBase.transform.InverseTransformDirection(camForward);

                        if (Mathf.Abs(localFwd.x) >= Mathf.Abs(localFwd.z))
                        {
                            int signX = localFwd.x >= 0f ? 1 : -1;
                            gridNudge.x = Plugin.nudgeZ * signX;
                            gridNudge.z = Plugin.nudgeX * signX;
                        }
                        else
                        {
                            int signZ = localFwd.z >= 0f ? 1 : -1;
                            gridNudge.z = Plugin.nudgeZ * signZ;
                            gridNudge.x = Plugin.nudgeX * signZ;
                        }
                    }

                    if (hasVertical)
                        gridNudge.y = Plugin.verticalOffset;

                    Int3 currentOffset = __instance.targetOffset;
                    Int3 adjustedOffset = currentOffset + gridNudge;

                    Int3 size = Base.CellSize[(uint)Base.CellType.Foundation];
                    Int3 maxs = adjustedOffset + size - 1;
                    bool valid = true;
                    foreach (Int3 pos in new Int3.Bounds(adjustedOffset, maxs))
                    {
                        if (targetBase.GetCell(pos) != Base.CellType.Empty || targetBase.IsCellUnderConstruction(pos))
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        __instance.targetOffset = adjustedOffset;
                        t.position = targetBase.GridToWorld(adjustedOffset);
                        t.rotation = targetBase.transform.rotation;
                        __result = true;
                    }
                }
            }
            else if (Plugin.freeSnapEnabled)
            {
                // Free placement: lock position, only arrow keys move it
                if (!Plugin.positionLocked)
                {
                    Plugin.lockedPosition = t.position;
                    Plugin.lockedRotation = t.rotation;
                    Plugin.positionLocked = true;
                }

                t.position = Plugin.lockedPosition + Plugin.accumulatedNudge;

                if (FindBasePatch.hasNearbyBase)
                    t.rotation = FindBasePatch.nearbyBaseRotation;
                else
                    t.rotation = Plugin.lockedRotation;
            }
            else if (targetBase == null && FindBasePatch.hasNearbyBase)
            {
                // Not in free mode but near a base with no attachment - align rotation
                Vector3 pos = t.position;
                t.rotation = FindBasePatch.nearbyBaseRotation;
                t.position = pos;
            }

            // === STEP 3: Rotation (always last, after position is finalized) ===
            if (Plugin.enableRotation.Value && Plugin.rotationStep != 0)
            {
                float angle = Plugin.rotationStep * 90f;
                Vector3 worldCenter = t.TransformPoint(Builder.aaBounds.center);
                t.RotateAround(worldCenter, Vector3.up, angle);
            }
        }
    }
}
