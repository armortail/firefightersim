using System;
using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Tunable parameters for confined-space oxygen (OxygenBox). Plain
    /// [Serializable] class so it shows in the Inspector and can be shared by
    /// all boxes without pulling in MonoBehaviour.
    /// </summary>
    [Serializable]
    public class OxygenConfig
    {
        [Tooltip("Oxygen a single burning cell inside the box consumes per tick.")]
        [Range(0f, 0.5f)]
        public float consumptionPerBurningCell = 0.02f;

        [Tooltip("Oxygen an OPEN box breathes back in per tick (door/window vented).")]
        [Range(0f, 0.5f)]
        public float replenishRate = 0.05f;

        [Tooltip("Oxygen level below which fire cannot sustain or ignite in the box.")]
        [Range(0f, 1f)]
        public float sustainThreshold = 0.15f;
    }
}
