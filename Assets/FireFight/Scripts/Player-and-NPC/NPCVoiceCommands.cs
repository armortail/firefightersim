using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;

namespace FireFight
{
    public class NPCVoiceCommands : MonoBehaviour

    {
        [SerializeField] private NPCMovement danny;
        [SerializeField] private NPCMovement simon;
        [SerializeField] private Camera commandCamera;
        [SerializeField] private float commandDistance = 100f;
        [SerializeField] private LayerMask commandLayers = Physics.DefaultRaycastLayers;

        private RaycastHit capturedHit;
        private bool hasCapturedHit;

        public void ExecuteCommand(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return;

            string command = Regex.Replace(
                transcript.ToLowerInvariant(), @"[^\w\s]", "");

            command = Regex.Replace(command, @"\s+", " ").Trim();
            string[] words = command.Split(' ');
            // Avoid turning "Danny, don't move here" into a movement order.
            if (words.Contains("dont") || words.Contains("not") ||
                words.Contains("cancel"))
            {
                Debug.Log("Command cancelled.");
                return;
            }

            // Don't guess which NPC should receive an ambiguous command.
            if (words.Contains("danny") && words.Contains("simon"))
            {
                Debug.Log("Name only one NPC.");
                return;
            }

            command = string.Join(" ", words.Where(word =>
                word == "danny" ||
                word == "simon" ||
                word == "move" ||
                word == "here"));

            Debug.Log("Filtered command: " + command);

            NPCMovement recipient;

            if (command == "danny move here")
            {
                recipient = danny;
            }
            else if (command == "simon move here")
            {
                recipient = simon;
            }
            else
            {
                Debug.Log("Unrecognized command: " + command);
                return;
            }

            if (recipient == null || !hasCapturedHit)
            {
                Debug.Log("Missing NPC or destination.");
                return;
            }

            if (Vector3.Dot(capturedHit.normal, Vector3.up) < 0.5f)
            {
                Debug.Log("Aim at walkable ground.");
                return;
            }

            if (!recipient.MoveTo(capturedHit.point))
            {
                Debug.Log("That destination is not reachable.");
            }
        }


        public void CaptureAim()
        {
            hasCapturedHit = false;

            if (commandCamera == null)
                return;

            Ray aimRay = commandCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            hasCapturedHit = Physics.Raycast(
                aimRay, out capturedHit, commandDistance,
                commandLayers, QueryTriggerInteraction.Ignore);
        }


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
