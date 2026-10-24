using System;
using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Tunable parameters for the smoke simulation. Plain [Serializable] class
    /// so it shows up in the Inspector and can be handed to the (non-Unity)
    /// SmokeSim without dragging MonoBehaviour along.
    /// </summary>
    [Serializable]
    public class SmokeConfig
    {
        [Header("Production")]
        [Tooltip("Smoke a Burning cell adds to itself each tick.")]
        [Range(0f, 1f)]
        public float smokePerBurningTick = 0.25f;

        [Tooltip("Smoke a Burned (smouldering) cell adds to itself each tick.")]
        [Range(0f, 1f)]
        public float smokePerSmolderingTick = 0.05f;

        [Header("Movement")]
        [Tooltip("Highest Y layer smoke is allowed to rise to. Smoke at this layer " +
                 "banks sideways instead of rising, as if hitting a ceiling. " +
                 "Set to -1 for no cap (smoke rises to the top of the grid).")]
        public int maxSmokeLayer = -1;

        [Tooltip("Fraction of a cell's smoke that tries to rise to the cell above each tick.")]
        [Range(0f, 1f)]
        public float riseRate = 0.6f;

        [Tooltip("Fraction that spreads sideways (per horizontal neighbour) when it cannot rise.")]
        [Range(0f, 0.25f)]
        public float spreadRate = 0.12f;

        [Header("Dissipation")]
        [Tooltip("Smoke removed from every cell each tick (natural clearing / thinning).")]
        [Range(0f, 0.2f)]
        public float dissipation = 0.01f;

        [Tooltip("Smoke below this is snapped to 0 to avoid endless tiny values.")]
        public float minSmoke = 0.001f;
    }
}
