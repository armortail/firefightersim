# Player and NPC recovery

Work is on `movement-player-recovery`, based on the updated `main` commit
`72b201c`. The original `movementandPlayer` branch is preserved.

## Try the restored scene

Open `Assets/FireFight/Scenes/SampleScene.unity`, press Play, and click the Game
view. Use WASD to walk and the mouse to look. Escape releases the cursor;
clicking the Game view locks it again.

The white dot marks the center of the camera view. Look at the floor and press
**N** to send the selected NPC there. Press N again while looking elsewhere to
replace that destination. Walls, ceilings, off-map positions, and destinations
without a complete navigation path are rejected.

The fire demo still uses **Space** to ignite the center cell and **left click**
to ignite the surface under the pointer. These are debug fire controls.

## How commands are connected

- `FirefighterMovement` on `player` handles first-person controls.
- `NPCCommandController` on `player` reads N and aims through its Command Camera.
  Its Selected Npc field specifies the recipient.
- `NPCMovement` on each NPC controls that NPC's NavMesh Agent. Its public
  `MoveTo(Vector3)` method accepts a destination and returns whether it could
  accept a complete route. NPCs do not read keyboard input themselves.
- `CommandHUD/AimDot` is a centered UI image; it does not intercept clicks.
- The extra child object named `FirefighterMovement` remains disabled.

For another NPC, duplicate the NPC root, place it on the baked floor, and set
the player's Selected Npc field to the intended recipient. Only that recipient
receives commands. `SelectNpc` also exposes selection to future code.

Voice recording and Hugging Face integration have not been implemented. A
future voice interpreter can share `CommandSelectedNpcAtAim()` or send a
world position to the selected NPC's `MoveTo` method.

## Keep the setup working

After changing floor or obstacle geometry, select Plane and bake its NavMesh
Surface again. Save the scene outside Play mode and commit its navigation
asset along with it. Preserve Unity `.meta` files when moving assets.

`FireFight.asmdef` references both Cinemachine and the Input System. The fire
demo uses the new keyboard and mouse APIs, with device null checks first.
The URP material tool now has its required editor namespaces and assembly
reference; no project materials were converted during recovery.

The Building scene and the incoming fire/smoke changes from main are preserved.
