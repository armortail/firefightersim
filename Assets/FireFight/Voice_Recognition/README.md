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

NPC commands are not connected yet. When adding a command script under
`Assets/FireFight/Scripts/Player-and-NPC`, add `com.whisper.unity` to the
FireFight assembly definition's references before using Whisper types there.
