using System;
using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// All tunable parameters for the fire simulation, grouped in one place.
    /// Plain [Serializable] class so it shows up in the Inspector and can be
    /// passed to the (non-Unity) simulation without dragging MonoBehaviour along.
    /// </summary>
    [Serializable]
    public class FireConfig
    {
        [Header("Simulation Speed")]
        [Tooltip("How many simulation steps run per second. Lower = slower fire.")]
        public float ticksPerSecond = 4f;

        [Header("Spread Tuning")]
        [Tooltip("Highest Y layer fire is allowed to spread to. Cells above this " +
                 "never ignite, so flames stay in a lower band while smoke can go " +
                 "higher. Set to -1 for no cap.")]
        public int maxFireLayer = -1;

        [Tooltip("Heat a burning cell pushes into each neighbour per tick.")]
        public float heatPerTick = 0.35f;

        [Tooltip("Heat level at which an unburned cell ignites.")]
        public float ignitionThreshold = 1f;

        [Tooltip("Chance (0..1) per tick that a hot neighbour actually ignites. Adds natural randomness.")]
        [Range(0f, 1f)]
        public float spreadChance = 0.6f;

        [Tooltip("Fuel a burning cell consumes per tick. Higher = burns out faster.")]
        public float burnRate = 0.05f;

        [Tooltip("Heat lost per tick when a cell is NOT next to fire (natural cooling).")]
        public float coolingRate = 0.1f;

        /// <summary>Seconds between ticks, derived from ticksPerSecond.</summary>
        public float TickInterval => 1f / Mathf.Max(0.01f, ticksPerSecond);
    }
}
