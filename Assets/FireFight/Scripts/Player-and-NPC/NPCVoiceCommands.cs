using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;

namespace FireFight
{
    public class NPCVoiceCommands : MonoBehaviour

    {
        [SerializeField] private NPCMovement danny;
        [SerializeField] private NPCMovement simon;
        [SerializeField] private NPCDestinationMarkers destinationMarkers;

        

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

            if (recipient == null)
            {
                Debug.Log("The requested NPC is not assigned.");
                return;
            }

            if (destinationMarkers == null)
            {
                Debug.Log("Assign the destination marker controller.");
                return;
            }

            if (!destinationMarkers.TryGetDestination(
                    recipient, out Vector3 destination))
            {
                Debug.Log("Place " + recipient.name + "'s marker first.");
                return;
            }

            if (!recipient.MoveTo(destination))
            {
                Debug.Log("That destination is no longer reachable.");
            }
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
