using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using FireFight.Managers;

namespace FireFight
{
    /// <summary>Routes completed speech to one named NPC, or an explicit everyone command.</summary>
    public class NPCVoiceCommands : MonoBehaviour
    {
        [SerializeField] private NPCMovement danny;
        [SerializeField] private NPCMovement simon;
        [SerializeField] private NPCDestinationMarkers destinationMarkers;

        public enum Recipient { Danny, Simon, Everyone }
        public enum Order { Move, FightFire, StopSpraying }

        public static bool TryParseCommand(string transcript, out Recipient recipient, out Order order)
        {
            recipient = default;
            order = default;
            if (string.IsNullOrWhiteSpace(transcript)) return false;
            string text = Regex.Replace(transcript.ToLowerInvariant().Replace('?', '\''), @"[^\w\s]", "");
            string[] words = Regex.Split(text.Trim(), @"\s+");
            if (words.Any(word => word == "dont" || word == "not" || word == "never" ||
                word == "cancel" || word == "cant" || word == "cannot" || word == "except"))
                return false;

            bool hasDanny = words.Contains("danny");
            bool hasSimon = words.Contains("simon");
            bool hasEveryone = words.Contains("everyone") || words.Contains("both");
            if ((hasDanny ? 1 : 0) + (hasSimon ? 1 : 0) + (hasEveryone ? 1 : 0) != 1)
                return false;
            recipient = hasDanny ? Recipient.Danny : hasSimon ? Recipient.Simon : Recipient.Everyone;

            string command = string.Join(" ", words.Where(word =>
                word == "move" || word == "here" || word == "fight" || word == "the" ||
                word == "fire" || word == "extinguish" || word == "spray" ||
                word == "shoot" || word == "water" || word == "stop" || word == "spraying"));
            switch (command)
            {
                case "move here":
                    order = Order.Move;
                    return recipient != Recipient.Everyone;
                case "fight the fire":
                case "fight fire":
                case "extinguish the fire":
                case "extinguish fire":
                case "spray water":
                case "shoot water":
                    order = Order.FightFire;
                    return true;
                case "stop spraying":
                case "stop water":
                case "stop spraying water":
                    order = Order.StopSpraying;
                    return true;
                default:
                    return false;
            }
        }

        public void ExecuteCommand(string transcript) => TryExecuteCommand(transcript);

        public bool TryExecuteCommand(string transcript)
        {
            if (!TryParseCommand(transcript, out Recipient recipient, out Order order))
            {
                Debug.Log("Unrecognized or ambiguous voice command: " + transcript, this);
                return false;
            }

            if (recipient == Recipient.Danny) return Issue(danny, order);
            if (recipient == Recipient.Simon) return Issue(simon, order);

            bool dannyAccepted = Issue(danny, order);
            bool simonAccepted = simon != danny && Issue(simon, order);
            return dannyAccepted || simonAccepted;
        }

        private bool Issue(NPCMovement npc, Order order)
        {
            if (npc == null)
            {
                Debug.Log("The requested NPC is not assigned.", this);
                return false;
            }

            if (order == Order.StopSpraying)
            {
                npc.CancelExtinguish();
                Debug.Log(npc.name + ": stopped firefighting.", this);
                return true;
            }

            if (order == Order.FightFire)
            {
                if (destinationMarkers == null ||
                    !destinationMarkers.TryGetFireTarget(npc, out Flammable burningObject))
                {
                    Debug.Log("Place " + npc.name + "'s marker on or near a burning object first.", this);
                    return false;
                }
                bool accepted = npc.ExtinguishAt(burningObject);
                Debug.Log(npc.name + (accepted ? ": fighting the marked fire." :
                    ": cannot reach a safe spraying position for that fire."), this);
                return accepted;
            }

            if (destinationMarkers == null ||
                !destinationMarkers.TryGetDestination(npc, out Vector3 destination))
            {
                Debug.Log("Place " + npc.name + "'s movement marker first.", this);
                return false;
            }
            return npc.MoveTo(destination);
        }
    }
}
