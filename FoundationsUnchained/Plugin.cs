using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FoundationsUnchained
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("Tobey.SnapBuilder", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        private Harmony harmony;

        // Rotation state
        internal static int rotationStep = 0; // 0-3 representing 0/90/180/270 degrees

        // Snapping state
        internal static int verticalOffset = 0;
        internal static int nudgeX = 0;
        internal static int nudgeZ = 0;
        internal static bool freeSnapEnabled = false;

        // Locked position for free placement (camera/player won't move the ghost)
        internal static bool positionLocked = false;
        internal static Vector3 lockedPosition;
        internal static Quaternion lockedRotation;
        internal static Vector3 accumulatedNudge = Vector3.zero; // World-space accumulated offset


        // Key repeat for held arrow keys
        private float nudgeRepeatTimer = 0f;
        private const float nudgeRepeatDelay = 0.4f;  // Initial delay before repeat starts
        private const float nudgeRepeatRate = 0.05f;   // Time between repeats once held
        private bool nudgeKeyHeld = false;
        internal static bool capturePositionNextFrame = false; // Signal to capture ghost position

        // Config - Rotation
        internal static ConfigEntry<bool> enableRotation;

        // Config - Snapping
        internal static ConfigEntry<float> snapSearchRange;
        internal static ConfigEntry<bool> enableVerticalSnap;
        internal static ConfigEntry<KeyCode> snapUpKey;
        internal static ConfigEntry<KeyCode> snapDownKey;
        internal static ConfigEntry<bool> enableGridNudge;
        internal static ConfigEntry<KeyCode> nudgeForwardKey;
        internal static ConfigEntry<KeyCode> nudgeBackKey;
        internal static ConfigEntry<KeyCode> nudgeLeftKey;
        internal static ConfigEntry<KeyCode> nudgeRightKey;
        internal static ConfigEntry<KeyCode> toggleFreeSnapKey;
        internal static ConfigEntry<KeyCode> resetNudgeKey;
        internal static ConfigEntry<float> nudgeStepSize;

        private void Awake()
        {
            Logger = base.Logger;

            // Rotation config
            enableRotation = Config.Bind(
                "Rotation",
                "EnableRotation",
                true,
                "Enable foundation rotation with mouse scroll wheel."
            );

            // Snapping config
            snapSearchRange = Config.Bind(
                "Snapping",
                "SnapSearchRange",
                40f,
                "Maximum distance for snapping foundations to existing bases."
            );

            enableVerticalSnap = Config.Bind(
                "Snapping",
                "EnableVerticalSnap",
                true,
                "Enable vertical offset control with PageUp/PageDown."
            );

            snapUpKey = Config.Bind(
                "Snapping",
                "SnapUpKey",
                KeyCode.PageUp,
                "Key to shift foundation placement up."
            );

            snapDownKey = Config.Bind(
                "Snapping",
                "SnapDownKey",
                KeyCode.PageDown,
                "Key to shift foundation placement down."
            );

            enableGridNudge = Config.Bind(
                "Snapping",
                "EnableGridNudge",
                true,
                "Enable arrow key nudging to fine-tune grid position when snapping."
            );

            nudgeForwardKey = Config.Bind(
                "Snapping",
                "NudgeForwardKey",
                KeyCode.UpArrow,
                "Key to nudge foundation forward on the grid."
            );

            nudgeBackKey = Config.Bind(
                "Snapping",
                "NudgeBackKey",
                KeyCode.DownArrow,
                "Key to nudge foundation backward on the grid."
            );

            nudgeLeftKey = Config.Bind(
                "Snapping",
                "NudgeLeftKey",
                KeyCode.LeftArrow,
                "Key to nudge foundation left on the grid."
            );

            nudgeRightKey = Config.Bind(
                "Snapping",
                "NudgeRightKey",
                KeyCode.RightArrow,
                "Key to nudge foundation right on the grid."
            );

            toggleFreeSnapKey = Config.Bind(
                "Snapping",
                "ToggleFreeSnapKey",
                KeyCode.F,
                "Key to toggle free grid placement (disables snap alignment)."
            );

            resetNudgeKey = Config.Bind(
                "Snapping",
                "ResetNudgeKey",
                KeyCode.R,
                "Key to reset all offsets (rotation, vertical, nudge) to zero."
            );

            nudgeStepSize = Config.Bind(
                "Snapping",
                "NudgeStepSize",
                0.1f,
                "World-space distance (meters) per arrow key press. Smaller = finer control."
            );

            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} loaded!");
        }

        private void Update()
        {
            // Only process input when builder is active
            if (!Builder.isPlacing)
            {
                // Reset state when not building
                if (rotationStep != 0 || verticalOffset != 0 || freeSnapEnabled || nudgeX != 0 || nudgeZ != 0)
                {
                    rotationStep = 0;
                    verticalOffset = 0;
                    nudgeX = 0;
                    nudgeZ = 0;
                    freeSnapEnabled = false;
                    positionLocked = false;
                    accumulatedNudge = Vector3.zero;
                }
                return;
            }

            // Check if we're placing a foundation
            if (Builder.lastTechType != TechType.BaseFoundation)
                return;

            // Don't process input when in intro/loading
            if (uGUI.isIntro)
                return;

            // Rotation via mouse scroll wheel
            if (enableRotation.Value)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll > 0f)
                {
                    rotationStep = (rotationStep + 1) % 4;
                    ErrorMessage.AddMessage($"Foundation rotation: {rotationStep * 90}\u00B0");
                }
                else if (scroll < 0f)
                {
                    rotationStep = (rotationStep + 3) % 4;
                    ErrorMessage.AddMessage($"Foundation rotation: {rotationStep * 90}\u00B0");
                }
            }

            // Vertical offset via PageUp/PageDown
            if (enableVerticalSnap.Value)
            {
                float vertStep = nudgeStepSize.Value;
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                    vertStep *= 0.5f;

                if (Input.GetKeyDown(snapUpKey.Value))
                {
                    if (freeSnapEnabled)
                    {
                        accumulatedNudge.y += vertStep;
                    }
                    else
                    {
                        verticalOffset++;
                    }
                    ErrorMessage.AddMessage($"Vertical offset: {(freeSnapEnabled ? accumulatedNudge.y.ToString("F1") + "m" : verticalOffset.ToString())}");
                }
                else if (Input.GetKeyDown(snapDownKey.Value))
                {
                    if (freeSnapEnabled)
                    {
                        accumulatedNudge.y -= vertStep;
                    }
                    else
                    {
                        verticalOffset--;
                    }
                    ErrorMessage.AddMessage($"Vertical offset: {(freeSnapEnabled ? accumulatedNudge.y.ToString("F1") + "m" : verticalOffset.ToString())}");
                }
            }

            // Grid nudge via arrow keys
            if (enableGridNudge.Value)
            {
                float step = nudgeStepSize.Value;
                // Hold Alt to halve the step for finer control
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                    step *= 0.5f;

                // Use the nearby base's axes for nudge directions.
                // This keeps movement aligned with the structure's grid.
                // Snap camera forward to the nearest base axis so there's no diagonal drift.
                Transform cam = MainCamera.camera.transform;
                Vector3 camFwd = cam.forward;
                camFwd.y = 0f;
                camFwd.Normalize();

                // Get the base's local X and Z axes projected to horizontal
                Quaternion baseRot = Patches.FindBasePatch.hasNearbyBase
                    ? Patches.FindBasePatch.nearbyBaseRotation
                    : (positionLocked ? lockedRotation : Quaternion.identity);

                Vector3 baseX = baseRot * Vector3.right;
                baseX.y = 0f;
                baseX.Normalize();
                Vector3 baseZ = baseRot * Vector3.forward;
                baseZ.y = 0f;
                baseZ.Normalize();

                // Find which base axis the camera is most aligned with
                float dotX = Vector3.Dot(camFwd, baseX);
                float dotZ = Vector3.Dot(camFwd, baseZ);

                Vector3 camForward, camRight;
                if (Mathf.Abs(dotX) >= Mathf.Abs(dotZ))
                {
                    // Camera mostly looks along base X axis
                    camForward = dotX >= 0f ? baseX : -baseX;
                    camRight = dotX >= 0f ? -baseZ : baseZ;
                }
                else
                {
                    // Camera mostly looks along base Z axis
                    camForward = dotZ >= 0f ? baseZ : -baseZ;
                    camRight = dotZ >= 0f ? baseX : -baseX;
                }

                // Determine which nudge key is pressed (if any)
                bool fwd = Input.GetKey(nudgeForwardKey.Value);
                bool back = Input.GetKey(nudgeBackKey.Value);
                bool left = Input.GetKey(nudgeLeftKey.Value);
                bool right = Input.GetKey(nudgeRightKey.Value);
                bool anyNudgeKey = fwd || back || left || right;

                // Handle repeat timing
                bool doNudge = false;
                if (anyNudgeKey)
                {
                    if (Input.GetKeyDown(nudgeForwardKey.Value) || Input.GetKeyDown(nudgeBackKey.Value) ||
                        Input.GetKeyDown(nudgeLeftKey.Value) || Input.GetKeyDown(nudgeRightKey.Value))
                    {
                        // First press - act immediately and start delay timer
                        doNudge = true;
                        nudgeRepeatTimer = nudgeRepeatDelay;
                        nudgeKeyHeld = true;
                    }
                    else if (nudgeKeyHeld)
                    {
                        // Held - count down timer
                        nudgeRepeatTimer -= Time.deltaTime;
                        if (nudgeRepeatTimer <= 0f)
                        {
                            doNudge = true;
                            nudgeRepeatTimer = nudgeRepeatRate;
                        }
                    }
                }
                else
                {
                    nudgeKeyHeld = false;
                    nudgeRepeatTimer = 0f;
                }

                if (doNudge)
                {
                    // Pick the first held direction (priority: fwd > back > left > right)
                    if (fwd)
                    {
                        if (freeSnapEnabled)
                            accumulatedNudge += camForward * step;
                        else
                            nudgeZ++;
                    }
                    else if (back)
                    {
                        if (freeSnapEnabled)
                            accumulatedNudge -= camForward * step;
                        else
                            nudgeZ--;
                    }
                    else if (left)
                    {
                        if (freeSnapEnabled)
                            accumulatedNudge -= camRight * step;
                        else
                            nudgeX--;
                    }
                    else if (right)
                    {
                        if (freeSnapEnabled)
                            accumulatedNudge += camRight * step;
                        else
                            nudgeX++;
                    }
                }
            }

            // Toggle free placement mode (fine positioning, aligned to nearby base)
            if (Input.GetKeyDown(toggleFreeSnapKey.Value))
            {
                if (!freeSnapEnabled)
                {
                    // Capture the current ghost position BEFORE switching to free mode.
                    // The ghost is currently at its snapped position.
                    var ghost = UnityEngine.Object.FindObjectOfType<ConstructableBase>();
                    if (ghost != null)
                    {
                        lockedPosition = ghost.transform.position;
                        lockedRotation = ghost.transform.rotation;
                        positionLocked = true;
                    }
                }
                else
                {
                    positionLocked = false;
                }
                freeSnapEnabled = !freeSnapEnabled;
                accumulatedNudge = Vector3.zero;
                if (!freeSnapEnabled)
                {
                    // Reset nudge when leaving free placement
                    nudgeX = 0;
                    nudgeZ = 0;
                    verticalOffset = 0;
                }
                ErrorMessage.AddMessage(freeSnapEnabled
                    ? "Free placement ON - use arrow keys, PageUp/Down, scroll wheel only"
                    : "Free placement OFF - attached to base grid");
            }

            // Reset all offsets
            if (Input.GetKeyDown(resetNudgeKey.Value))
            {
                rotationStep = 0;
                verticalOffset = 0;
                nudgeX = 0;
                nudgeZ = 0;
                accumulatedNudge = Vector3.zero;
                if (positionLocked)
                {
                    // Re-lock at original position
                    positionLocked = false;
                }
                ErrorMessage.AddMessage("All offsets reset");
            }
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
