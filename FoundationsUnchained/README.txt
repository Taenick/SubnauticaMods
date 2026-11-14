Foundations Unchained v1.0.0
============================

A Subnautica mod that unlocks foundation placement controls: rotation, extended
snapping, vertical offset, and free grid placement.

Compatible with SnapBuilder.


FEATURES
--------

* Foundation Rotation
  Scroll the mouse wheel while placing a foundation to rotate it in 90-degree
  increments (0, 90, 180, 270 degrees). Only applies when free-placing
  foundations (not snapping to an existing base, where the base grid orientation
  is used).

* Extended Snap Range
  Foundations can snap to existing bases from further away (configurable, default
  40 meters vs vanilla 20 meters).

* Vertical Offset Control
  Press PageUp/PageDown while placing a foundation to shift it up or down on the
  base grid. Allows multi-level foundation stacking at arbitrary heights.

* Free Grid Placement
  Press F to toggle free snap mode. When enabled, foundations placed on an
  existing base ignore automatic snap alignment, allowing staggered and offset
  layouts.


REQUIREMENTS
------------

* Subnautica (Steam)
* BepInEx 5.4.21 for Subnautica
  https://www.nexusmods.com/subnautica/mods/1108


INSTALLATION
------------

1. Install BepInEx 5 for Subnautica if you haven't already.
2. Copy the FoundationsUnchained.dll file into:
   Subnautica/BepInEx/plugins/FoundationsUnchained/
3. Launch Subnautica. The mod loads automatically.


CONFIGURATION
-------------

After the first launch, a config file is generated at:
  Subnautica/BepInEx/config/FoundationsUnchained.cfg

Settings:

  [Rotation]
  EnableRotation = true         Enable/disable foundation rotation

  [Snapping]
  SnapSearchRange = 40          Max snap distance (meters)
  EnableVerticalSnap = true     Enable/disable vertical offset control
  SnapUpKey = PageUp            Key to shift placement up
  SnapDownKey = PageDown        Key to shift placement down
  ToggleFreeSnapKey = F         Key to toggle free grid placement


CONTROLS
--------

  Mouse Scroll Wheel    Rotate foundation (when free-placing)
  PageUp                Shift foundation up one grid level
  PageDown              Shift foundation down one grid level
  F                     Toggle free snap mode on/off


COMPATIBILITY
-------------

* Works alongside SnapBuilder - both mods patch different game systems and do
  not conflict.
* May conflict with other mods that modify BaseAddCellGhost or BaseGhost.


LICENSE
-------

MIT License - see LICENSE file in the repository.
