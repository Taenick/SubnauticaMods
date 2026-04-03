Delete On Sight - Subnautica Mod
=================================

Look at it. Delete it. No questions asked.

FEATURES
--------
- Press DELETE to destroy whatever you're looking at (requires the object to have collision)
- Press ALT+DELETE for area delete (5m radius), use this for objects WITHOUT collision
  like small plants, cables, wreck decorations, and other visual-only objects
- Type "delete" in the dev console for single target delete
- Works on: base pieces, vehicles, creatures, world objects, wall-mounted items
- Base pieces are force-removed even if the game says "attached components must be deconstructed first"
- Wall-mounted items (lockers, signs, etc.) inside deleted rooms are auto-cleaned
- Base pieces and constructable items refund their crafting materials on deletion
- Storage containers (lockers, etc.) drop their contents before being deleted
- If your inventory is full, excess items and materials drop in front of you

TIP: If pressing DELETE doesn't work on an object, try ALT+DELETE instead.
     Some objects (plants, wires, small decorations) don't have collision
     and can only be removed with area delete.

PROTECTED OBJECTS (Cannot be deleted)
-------------------------------------
- The Player
- The Escape Pod / Life Pod
- Terrain / Ocean floor
- Terrain chunks (grass, ground layers, rock geometry)
- The vehicle you are currently piloting (exit first!)

CONFIGURATION
-------------
After first run, edit: BepInEx/config/DeletePlugin.cfg
- DeleteKey: Change the keybind (default: Delete)
- AreaDeleteRadius: Change the area delete radius in meters (default: 5)

INSTALLATION
------------
1. Requires BepInEx 5.x for Subnautica
2. Extract the zip so that BepInEx/plugins/DeleteOnSight/ ends up inside your Subnautica folder
3. Launch the game

PERSISTENCE
-----------
- Base pieces: Saved automatically by the game (permanent)
- Vehicles and creatures: Saved automatically by the game (permanent)
- Static world objects: Saved to deleted-objects.json in your save folder
  Delete this file to restore all deleted world objects

!! CAUTION !!
-------------
- Base pieces and constructable items refund materials, but vehicles, creatures,
  and world objects do NOT return resources
- Area delete does NOT refund resources or drop storage contents
- When inventory is full, items drop in front of you (~3m), look around if you
  don't see them immediately
- ALWAYS back up your save files before using this mod!
  Save location: Subnautica/SNAppData/SavedGames/
- If you delete too much, reload your last save (save scum!)
- Area delete (ALT+DELETE) removes ALL objects in the radius, aim carefully
- Deleting base pieces that support other structures may cause visual glitches
- This mod bypasses the game's safety checks, that's the point, but use responsibly

UNINSTALL
---------
Delete the BepInEx/plugins/DeleteOnSight/ folder.
To restore deleted world objects, also delete deleted-objects.json from your save folder.