# Local speech recognition

Whisper Unity 1.4.0 is installed in `Whisper/`. Source:
https://github.com/Macoron/whisper.unity
Upstream commit: `529a628a915a97799e89e061af9cb7c71407124d`.

The included multilingual tiny model is at
`Assets/StreamingAssets/Whisper/ggml-tiny.bin`. StreamingAssets is required so
the model remains a normal file available to the native recognizer in builds.
The default WhisperManager settings already point to this location.

## First microphone test

1. Save your current scene.
2. Open `Examples/2 - Microphone/2 - Microphone.unity`.
3. Enter Play mode and allow the model to load.
4. Click Record, speak a short phrase, then click Stop.
5. Read the transcription shown in the example UI.

The model runs locally. No Hugging Face account, API key, or hosted inference
subscription is needed. First test on Windows x64 with GPU acceleration off.
Microphone access must be enabled for Unity in Windows settings.

## Project adaptations

- Added the UnityEngine.UI assembly reference required by the runtime UI helper.
- Added a separate assembly for the example scripts.
- Restricted each desktop native plugin to its actual OS and architecture.
- Updated the example EventSystems to use the project's new Input System.
- Kept the upstream runtime code and asset GUIDs intact.

The original README and MIT license are in `Whisper/`. Native binaries and
model weights are tracked with the repository's existing Git LFS setup.

## NPC commands in TestScene

- Left-click: place Danny's red movement marker.
- Right-click: place Simon's blue movement marker.
- Hold Space, speak, and release Space to issue one completed command.
- `Danny move here` / `Simon move here`: use that NPC's movement marker.
- `Danny fight the fire` / `Simon fight the fire`: find a burning object near that NPC's own red/blue ping, approach and spray.
- `Danny stop spraying` / `Simon stop spraying`: cancel that NPC's firefighting job and approach.
- `Everyone fight the fire` / `Everyone stop spraying`: explicitly address both NPCs.

`Spray water`, `shoot water`, and `extinguish the fire` are also accepted after
an NPC name. They require a designated object that is still burning in the actual
fire grid. Place the red/blue marker on a burning object or on reachable ground
near it; the same ping works for movement and firefighting. There is no middle-click
target. The NPC continues through burning objects within 2.5 metres of the initial
fire and briefly soaks an extinguished surface before moving to the next one. Orders
without a recipient, ambiguous recipients, and negated orders are ignored.

TestScene contains a seeded burning crate, a nearby flammable crate and a cold
crate across an air gap. It uses the existing FireManager, Flammable and
SeedIgniter components. FireTestFeedback is a test-only visual adapter that reads
burning/smoke state; it does not implement or modify fire physics. Re-enter Play
mode to restart the test fire. The other scenes and shared NPC prefabs are not
changed by this setup.

Water uses continuous droplets with a narrow jet core or a widening cone matching
the hose's configured half-angle. Water stops at solid colliders and only cools/wets
surfaces it can actually reach. The fire and smoke simulation and fire visuals are
unchanged by the water update.
