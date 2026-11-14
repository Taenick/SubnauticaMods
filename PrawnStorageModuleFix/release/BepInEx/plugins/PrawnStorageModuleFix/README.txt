PrawnStorageModuleFix
=====================

Fixes the Prawn Suit (Exosuit) storage module stacking bug in Subnautica.

In the vanilla game, equipping multiple Vehicle Storage Modules in the Prawn Suit
does not increase storage capacity beyond the first module. This mod fixes that so
each additional storage module correctly adds 16 storage slots, matching the wiki
specification for the Vehicle Storage Module.

Storage Capacity
----------------
  0 modules: 16 slots (8x2)
  1 module:  32 slots (8x4)
  2 modules: 48 slots (8x6)
  3 modules: 64 slots (8x8)
  4 modules: 80 slots (8x10, scrollable)

When total storage exceeds 64 slots, a scrollbar appears so all items remain
accessible without the UI extending off screen.

Better Vehicle Storage Compatibility
------------------------------------
  Fully compatible with the Better Vehicle Storage mod. Tiered storage modules
  (Mk2 through Mk5) are detected automatically and their capacities stack
  correctly alongside vanilla modules.

Requirements
------------
  - Subnautica
  - BepInEx 5.4.21

Installation
------------
  1. Install BepInEx 5 for Subnautica if you haven't already.
  2. Copy PrawnStorageModuleFix.dll into:
     Subnautica\BepInEx\plugins\PrawnStorageModuleFix\

Uninstallation
--------------
  Delete the PrawnStorageModuleFix folder from BepInEx\plugins\.

Compatibility
-------------
  - Safe to install on existing saves.
  - Items in storage are not affected when adding or removing the mod.
  - Compatible with Better Vehicle Storage (Purple Edition).
