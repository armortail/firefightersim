using UnityEngine;
using FireFight.Managers;

namespace FireFight
{
    /// <summary>TestScene-only visuals. Reads the existing simulation; never changes fire physics.</summary>
    [RequireComponent(typeof(Flammable))]
    public class FireTestFeedback : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] flames;
        [SerializeField] private ParticleSystem smoke;
        private Flammable flammable;
        private bool wasBurning;
        public float SmokeDensity { get; private set; }

        private void Awake()
        {
            flammable = GetComponent<Flammable>();
            SetFlames(false);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            bool burning = NPCFireTarget.IsBurning(flammable);
            if (burning != wasBurning)
            {
                SetFlames(burning);
                wasBurning = burning;
            }

            SmokeDensity = 0f;
            if (flammable != null && flammable.fireManager != null && flammable.fireManager.Grid != null)
            {
                var grid = flammable.fireManager.Grid;
                for (int i = 0; i < 4; i++)
                {
                    var cell = grid.GetCell(grid.WorldToGrid(transform.position + Vector3.up * i * grid.CellSize));
                    if (cell != null) SmokeDensity = Mathf.Max(SmokeDensity, cell.smoke);
                }
            }

            if (smoke == null) return;
            var emission = smoke.emission;
            emission.rateOverTime = SmokeDensity * 25f;
            if (SmokeDensity > 0.001f && !smoke.isPlaying) smoke.Play();
            else if (SmokeDensity <= 0.001f && smoke.isPlaying)
                smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void SetFlames(bool active)
        {
            if (flames == null) return;
            foreach (var system in flames)
            {
                if (system == null) continue;
                if (active) system.Play(false);
                else system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDisable()
        {
            SetFlames(false);
            wasBurning = false;
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
