using UnityEngine;
using UnityEngine.InputSystem;
using Whisper;
using Whisper.Utils;
using UnityEngine.Scripting.APIUpdating;

namespace FireFight
{
    [MovedFrom(true, sourceNamespace: "FireFight", sourceAssembly: "FireFight", sourceClassName: "VoiceController")]
    public class VoiceInputController : MonoBehaviour
    {
        [SerializeField] private WhisperManager whisper;
        [SerializeField] private MicrophoneRecord microphoneRecord;
        [SerializeField] private NPCVoiceCommands commandRouter;

        private bool isTranscribing;
        private int recordingVersion;


        private void OnEnable()
        {
            if (whisper == null || microphoneRecord == null || commandRouter == null)
            {
                Debug.LogError("Assign all VoiceInputController references.", this);
                enabled = false;
                return;
            }

            microphoneRecord.OnRecordStop += OnRecordStop;
        }

        private void OnDisable()
        {
            recordingVersion++;

            if (microphoneRecord == null)
                return;

            microphoneRecord.OnRecordStop -= OnRecordStop;

            if (microphoneRecord.IsRecording)
                microphoneRecord.StopRecord();
        }

        // Update is called once per frame
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || isTranscribing)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (!whisper.IsLoaded)
                {
                    Debug.Log("Voice model is still loading.");
                    return;
                }

                if (!microphoneRecord.IsRecording)
                {
                    microphoneRecord.StartRecord();
                    Debug.Log("Listening...");
                }
            }

            if (keyboard.spaceKey.wasReleasedThisFrame &&
                microphoneRecord.IsRecording)
            {
                microphoneRecord.StopRecord();
            }
        }

        private async void OnRecordStop(AudioChunk audio)
        {
            if (isTranscribing || audio.Data == null || audio.Data.Length == 0)
                return;

            int thisRecording = recordingVersion;
            isTranscribing = true;

            try
            {
                Debug.Log("Recognizing command...");

                var result = await whisper.GetTextAsync(
                    audio.Data, audio.Frequency, audio.Channels);

                if (this == null || !isActiveAndEnabled ||
                    thisRecording != recordingVersion || result == null)
                {
                    return;
                }

                Debug.Log("Heard: " + result.Result);
                commandRouter.ExecuteCommand(result.Result);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                isTranscribing = false;
            }
        }
    }
}
