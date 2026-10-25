using UnityEngine;
using FireFight.Environment;

namespace FireFight.Managers
{
    /// <summary>
    /// Unity entry point for the fire simulation. Responsibilities are kept
    /// narrow on purpose:
    ///   - build the grid from Inspector settings on Awake
    ///   - drive the simulation at a fixed tick rate in Update
    ///   - expose a small public API (ignite / extinguish / query)
    ///
    /// The actual storage lives in <see cref="FireGrid"/> and the spread rules
    /// in <see cref="FireSim"/>. This class just coordinates them, so it
    /// stays small and the pieces can be tested independently.
    ///
    /// Completely self-contained: it does not read or depend on scene art
    /// assets. It is its own invisible grid anchored at transform.position.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Number of cells along each axis (X, Y, Z). Y > 1 enables smoke to rise.")]
        public Vector3Int gridSize = new Vector3Int(20, 6, 20);

        [Tooltip("Size of each cell in world units (metres).")]
        public float cellSize = 1f;

        [Header("Tuning")]
        public FireConfig config = new FireConfig();
        public SmokeConfig smokeConfig = new SmokeConfig();
        public OxygenConfig oxygenConfig = new OxygenConfig();

        [Header("Confined Spaces (Oxygen Boxes)")]
        [Tooltip("Enclosed grid regions with a shared oxygen pool. Fire inside " +
                 "depletes the air and suffocates until the box is opened. Grid " +
                 "coordinates, inclusive min..max.")]
        public OxygenBoxDef[] oxygenBoxes = new OxygenBoxDef[0];

        // Retained so door/ventilation scripts can assert the manual-box model is in
        // use. Auto-detection was removed; the manual HouseDoor/HouseVentilation system
        // is the one in use, so this stays false. Kept serialized to match existing scene data.
        [HideInInspector] public bool autoDetectOxygenBoxes = false;

        /// <summary>Inspector-friendly definition of an oxygen box (resolved to an OxygenBox at runtime).</summary>
        [System.Serializable]
        public class OxygenBoxDef
        {
            public string name = "Room";
            public Vector3Int min;
            public Vector3Int max = new Vector3Int(5, 3, 5);
            [Tooltip("If true the box starts vented (fresh air). If false it starts sealed.")]
            public bool startOpen = false;
        }

        [Header("Fuel")]
        [Tooltip("If true, every cell starts with fuel so fire can spread freely " +
                 "(Phase 1 testing). If false (default), cells start with NO fuel - " +
                 "air does not burn - and Flammable objects paint in their own fuel " +
                 "via AddFuelInBounds. Smoke still travels through air either way.")]
        public bool fillWithFuel = false;

        [Header("Debug")]
        [Tooltip("Log burning-cell count and burned fraction to the Console once per second. " +
                 "Off by default so it doesn't spam; turn on while tuning spread.")]
        public bool logSpreadStats = false;

        /// <summary>The underlying grid (read-only access for visualizers, etc.).</summary>
        public FireGrid Grid { get; private set; }

        private FireSim simulation;
        private SmokeSim smokeSimulation;
        private float tickTimer;
        private float statsLogTimer;

        // Runtime oxygen boxes, built from oxygenBoxes defs on Awake.
        private readonly System.Collections.Generic.List<OxygenBox> runtimeBoxes =
            new System.Collections.Generic.List<OxygenBox>();

        // Registry of flammable scene objects, so we can report how much of the
        // scene is on fire (for later win/loss logic). Flammable adds/removes itself.
        private readonly System.Collections.Generic.List<Flammable> flammables =
            new System.Collections.Generic.List<Flammable>();

        /// <summary>Called by Flammable.Start so the manager can track it.</summary>
        public void RegisterFlammable(Flammable f)
        {
            if (f != null && !flammables.Contains(f)) flammables.Add(f);
        }

        /// <summary>Called by Flammable.OnDestroy to stop tracking it.</summary>
        public void UnregisterFlammable(Flammable f)
        {
            flammables.Remove(f);
        }

        /// <summary>Total flammable objects registered in the scene.</summary>
        public int FlammableCount => flammables.Count;

        /// <summary>How many flammable objects are currently on fire.</summary>
        public int FlammableOnFireCount()
        {
            int n = 0;
            for (int i = 0; i < flammables.Count; i++)
                if (flammables[i] != null && flammables[i].inflames) n++;
            return n;
        }

        /// <summary>
        /// Fraction (0..1) of flammable objects currently on fire. This is the
        /// "how much of the scene is under fire" metric for later scoring.
        /// </summary>
        public float SceneOnFireFraction()
        {
            return flammables.Count == 0 ? 0f : (float)FlammableOnFireCount() / flammables.Count;
        }

        // ---- Oxygen box API (for door/window scripts to trigger backdraft) ----

        /// <summary>Number of oxygen boxes active at runtime.</summary>
        public int OxygenBoxCount => runtimeBoxes.Count;

        /// <summary>
        /// Open or seal an oxygen box by index. Opening a sealed, oxygen-starved
        /// box lets fresh air back in and the fire inside flares up (backdraft).
        /// </summary>
        public void SetOxygenBoxOpen(int index, bool open)
        {
            if (index >= 0 && index < runtimeBoxes.Count)
                runtimeBoxes[index].SetOpen(open);
        }

        /// <summary>Open/seal the box whose world-space point falls inside it. Returns false if none.</summary>
        public bool SetOxygenBoxOpenAtWorld(Vector3 worldPos, bool open)
        {
            Vector3Int p = Grid.WorldToGrid(worldPos);
            for (int i = 0; i < runtimeBoxes.Count; i++)
            {
                if (runtimeBoxes[i].Contains(p))
                {
                    runtimeBoxes[i].SetOpen(open);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Current oxygen (0..1) of a box by index, or -1 if index invalid.</summary>
        public float GetOxygen(int index)
        {
            return (index >= 0 && index < runtimeBoxes.Count) ? runtimeBoxes[index].Oxygen : -1f;
        }

        private void Awake()
        {
            Grid = new FireGrid(gridSize, cellSize, transform.position);

            simulation = new FireSim(Grid, config, runtimeBoxes);
            smokeSimulation = new SmokeSim(Grid, smokeConfig);

            // Air does not burn: unless we explicitly fill the grid with fuel,
            // every cell starts with zero fuel so fire cannot spread through
            // empty space. Smoke is unaffected and still travels everywhere.
            if (!fillWithFuel)
            {
                FireCell[] cells = Grid.Cells;
                for (int i = 0; i < cells.Length; i++) cells[i].fuel = 0f;
            }

            BuildOxygenBoxes();
        }

        /// <summary>Build the runtime oxygen boxes from the hand-authored <see cref="oxygenBoxes"/> defs.</summary>
        private void BuildOxygenBoxes()
        {
            runtimeBoxes.Clear();
            if (oxygenBoxes == null) return;
            foreach (var def in oxygenBoxes)
            {
                if (def == null) continue;
                runtimeBoxes.Add(new OxygenBox(def.min, def.max, oxygenConfig, def.startOpen));
            }
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;
            while (tickTimer >= config.TickInterval)
            {
                tickTimer -= config.TickInterval;
                simulation.Tick();
                smokeSimulation.Tick();
            }

            // Optional debug readout (moved here from the old FireDemo component).
            if (logSpreadStats)
            {
                statsLogTimer += Time.deltaTime;
                if (statsLogTimer >= 1f)
                {
                    statsLogTimer = 0f;
                    Debug.Log($"[FireFight] Burning cells: {CountBurning()} | " +
                              $"Burned fraction: {BurnedFraction():P0}");
                }
            }
        }

        // ---- Public API ----

        /// <summary>Ignite the cell at a grid position. False if out of bounds / no fuel.</summary>
        public bool IgniteCell(Vector3Int gridPos)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null || cell.fuel <= 0f) return false;
            cell.state = FireState.Burning;
            cell.heat = config.ignitionThreshold;
            return true;
        }

        /// <summary>Ignite whatever cell contains the given world-space point.</summary>
        public bool IgniteWorld(Vector3 worldPos)
        {
            return IgniteCell(Grid.WorldToGrid(worldPos));
        }

        /// <summary>Cool / put out a cell. Enough cooling stops an active burn.</summary>
        public void ExtinguishCell(Vector3Int gridPos, float cooling)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;
            cell.heat = Mathf.Max(0f, cell.heat - cooling);
            if (cell.state == FireState.Burning && cell.heat < config.ignitionThreshold)
            {
                cell.state = FireState.Unburned; // can relight if fuel remains
            }
        }

        // ---- Extinguishing: ways to kill fire ----
        //
        // All methods apply two effects to each affected cell:
        //   - cooling: removes heat (can drop a flame below ignition)
        //   - wetness: left behind so the cell resists re-igniting for a while
        // Tools (hose, extinguisher, sprinkler) call the shape that fits them.

        /// <summary>Cool AND wet a single cell. The building block for every area method.</summary>
        public void WetCell(Vector3Int gridPos, float cooling, float wetness)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;

            cell.heat = Mathf.Max(0f, cell.heat - cooling);
            cell.wetness = Mathf.Clamp01(cell.wetness + wetness);

            if (cell.state == FireState.Burning && cell.heat < config.ignitionThreshold)
                cell.state = FireState.Unburned; // can relight once it dries
        }

        // ---- Oxygen side: smother with foam / CO2 / blanket / sand ----
        //
        // Applies a persistent oxygen-blocking COATING instead of cooling. A cell
        // coated above FireConfig.coatingExtinguishThreshold is put out and cannot
        // reignite until the coating decays. Use for foam, CO2, dry powder, a fire
        // blanket, or shovelled sand/dirt.

        /// <summary>Lay smother coating on one cell (building block for the area shapes).</summary>
        public void SmotherCell(Vector3Int gridPos, float coating)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;

            cell.coating = Mathf.Clamp01(cell.coating + coating);

            // Immediate knockdown if we sealed it past the threshold.
            if (cell.state == FireState.Burning && cell.coating >= config.coatingExtinguishThreshold)
            {
                cell.heat = 0f;
                cell.state = FireState.Unburned; // can relight once the coating decays
            }
        }

        /// <summary>Smother a sphere of cells (foam burst / CO2 cloud / extinguisher). Falls off to the edge.</summary>
        public void SmotherSphere(Vector3 worldCenter, float radius, float coating)
        {
            ForEachCellInSphere(worldCenter, radius,
                (gp, falloff) => SmotherCell(gp, coating * falloff));
        }

        /// <summary>Smother every cell in a world-space box (foam blanket / total-flood CO2 zone).</summary>
        public void SmotherBounds(Bounds worldBounds, float coating)
        {
            ForEachCellInBounds(worldBounds, cell =>
                cell.coating = Mathf.Clamp01(cell.coating + coating));
            // Knock down any flames the blanket now seals.
            ForEachCellInBounds(worldBounds, cell =>
            {
                if (cell.state == FireState.Burning && cell.coating >= config.coatingExtinguishThreshold)
                {
                    cell.heat = 0f;
                    cell.state = FireState.Unburned;
                }
            });
        }

        // ---- Fuel side: starve the fire (firebreak / clear the fuel) ----
        //
        // Removes fuel from cells so there is nothing left to burn. A burning cell
        // whose fuel hits zero finishes as Burned (consumed); an unburned cell with
        // no fuel can never catch. Use for cutting a firebreak, hauling away stock,
        // or scraping a surface down to non-combustible material.

        /// <summary>Remove up to <paramref name="amount"/> fuel (0..1) from one cell.</summary>
        public void RemoveFuel(Vector3Int gridPos, float amount)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;

            cell.fuel = Mathf.Clamp01(cell.fuel - Mathf.Max(0f, amount));
            if (cell.fuel <= 0f)
            {
                cell.fuel = 0f;
                // Nothing left to burn: a flame here is starved out, an unburned
                // cell becomes inert. Mark Burned so it cannot relight.
                cell.heat = 0f;
                cell.state = FireState.Burned;
            }
        }

        /// <summary>Fully clear fuel from one cell (hard firebreak).</summary>
        public void StarveCell(Vector3Int gridPos) => RemoveFuel(gridPos, 1f);

        /// <summary>Cut a firebreak through a world-space box: clears all fuel inside it.</summary>
        public void StarveBounds(Bounds worldBounds)
        {
            ForEachCellInBounds(worldBounds, cell =>
            {
                cell.fuel = 0f;
                cell.heat = 0f;
                cell.state = FireState.Burned;
            });
        }

        // ---- Chain-reaction side: chemical knockdown (dry chem / clean agent) ----
        //
        // Interrupts the combustion chain reaction directly (like a dry-chemical or
        // Halon/clean-agent discharge): any burning cell is immediately knocked
        // down. This does NOT cool, coat, or remove fuel, so without follow-up the
        // cell can reignite from a hot neighbour - realistic for flammable-liquid
        // fires where the dry chem stops the flame but the hot fuel can flash back.

        /// <summary>Instantly knock a flame out of one cell (chain-reaction interruption).</summary>
        public void ChemicalSuppressCell(Vector3Int gridPos)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;
            if (cell.state == FireState.Burning)
            {
                cell.heat = 0f;
                cell.state = FireState.Unburned; // knocked down; may flash back if reheated
            }
        }

        /// <summary>Knock down every flame in a sphere (handheld extinguisher sweep).</summary>
        public void ChemicalSuppressSphere(Vector3 worldCenter, float radius)
        {
            ForEachCellInSphere(worldCenter, radius, (gp, _) => ChemicalSuppressCell(gp));
        }

        /// <summary>Knock down every flame in a world-space box (total-flood clean-agent dump).</summary>
        public void ChemicalSuppressBounds(Bounds worldBounds)
        {
            ForEachCellInBounds(worldBounds, cell =>
            {
                if (cell.state == FireState.Burning)
                {
                    cell.heat = 0f;
                    cell.state = FireState.Unburned;
                }
            });
        }

        /// <summary>
        /// Extinguish a sphere of cells around a world point (splash / burst /
        /// extinguisher cloud). Cooling and wetness fall off toward the edge.
        /// </summary>
        public void ExtinguishSphere(Vector3 worldCenter, float radius, float cooling, float wetness)
        {
            // Falloff-weighted water over a sphere, sharing the one sphere-iteration helper
            // (see ForEachCellInSphere) so every area tool scans cells the same way.
            ForEachCellInSphere(worldCenter, radius,
                (gp, falloff) => WetCell(gp, cooling * falloff, wetness * falloff));
        }

        /// <summary>
        /// Extinguish a cone of cells from an origin along a direction (a hose
        /// spray in Cone mode). A cell is affected if it is within range and the
        /// angle between the spray direction and the cell is under halfAngle.
        /// </summary>
        public void ExtinguishCone(Vector3 origin, Vector3 direction, float range,
            float halfAngleDegrees, float cooling, float wetness)
        {
            if (range <= 0f) return;
            direction = direction.normalized;
            float cosHalf = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);

            var min = new Vector3(origin.x - range, origin.y - range, origin.z - range);
            var max = new Vector3(origin.x + range, origin.y + range, origin.z + range);
            Vector3Int gMin = Grid.WorldToGrid(min);
            Vector3Int gMax = Grid.WorldToGrid(max);

            for (int x = gMin.x; x <= gMax.x; x++)
                for (int y = gMin.y; y <= gMax.y; y++)
                    for (int z = gMin.z; z <= gMax.z; z++)
                    {
                        var gp = new Vector3Int(x, y, z);
                        if (Grid.GetCell(gp) == null) continue;

                        Vector3 toCell = Grid.GridToWorld(gp) - origin;
                        float dist = toCell.magnitude;
                        if (dist > range || dist < 1e-4f) continue;

                        // Inside the cone if the angle is within the half-angle.
                        if (Vector3.Dot(toCell / dist, direction) < cosHalf) continue;

                        float falloff = 1f - (dist / range);
                        WetCell(gp, cooling * falloff, wetness * falloff);
                    }
        }

        /// <summary>
        /// Extinguish a jet: a focused stream. Cools/wets the single cell at the
        /// impact point plus its immediate neighbours (a small splash).
        /// </summary>
        public void ExtinguishJet(Vector3 impactPoint, float cooling, float wetness)
        {
            ExtinguishSphere(impactPoint, cellSize, cooling, wetness);
        }

        /// <summary>Extinguish every cell in a world-space box (sprinkler zone / retardant drop).</summary>
        public void ExtinguishBounds(Bounds worldBounds, float cooling, float wetness)
        {
            ForEachCellInBounds(worldBounds, cell =>
            {
                cell.heat = Mathf.Max(0f, cell.heat - cooling);
                cell.wetness = Mathf.Clamp01(cell.wetness + wetness);
                if (cell.state == FireState.Burning && cell.heat < config.ignitionThreshold)
                    cell.state = FireState.Unburned;
            });
        }

        public FireCell GetCell(Vector3Int gridPos) => Grid.GetCell(gridPos);

        /// <summary>
        /// Set a cell's fuel (0..1). Use this to mark where things can burn when
        /// fillWithFuel is off, e.g. paint fuel onto a region representing
        /// furniture or a confined, oxygen-fed space. A future oxygen system
        /// will hook in here to gate burning by available air.
        /// </summary>
        public void SetFuel(Vector3Int gridPos, float fuel)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell != null) cell.fuel = Mathf.Clamp01(fuel);
        }

        // ---- Bounds-based helpers: the bridge for physical objects ----
        //
        // A Flammable object knows its world-space Bounds (from its collider).
        // These methods translate that volume into grid cells so the object can
        // paint fuel into the cells it occupies and later ask if any are burning.
        // The bounds<->grid math lives here, keeping Flammable free of grid internals.

        /// <summary>
        /// Give fuel to every cell overlapping a world-space volume. Use when a
        /// flammable object registers itself so fire can exist where it stands.
        /// </summary>
        public void AddFuelInBounds(Bounds worldBounds, float fuel, float flammability)
        {
            ForEachCellInBounds(worldBounds, cell =>
            {
                cell.fuel = Mathf.Clamp01(fuel);
                cell.flammability = Mathf.Clamp01(flammability);
            });
        }

        /// <summary>Ignite every fuelled cell overlapping a world-space volume.</summary>
        public void IgniteBounds(Bounds worldBounds)
        {
            ForEachCellInBounds(worldBounds, cell =>
            {
                if (cell.fuel > 0f)
                {
                    cell.state = FireState.Burning;
                    cell.heat = config.ignitionThreshold;
                }
            });
        }

        /// <summary>True if ANY cell overlapping the volume is currently burning.</summary>
        public bool IsAnyBurningInBounds(Bounds worldBounds)
        {
            bool burning = false;
            ForEachCellInBounds(worldBounds, cell =>
            {
                if (cell.state == FireState.Burning) burning = true;
            });
            return burning;
        }

        /// <summary>
        /// True if EVERY fuelled cell in the volume has finished burning (Burned).
        /// Useful to know a flammable object is fully consumed. Returns false if
        /// the volume has no fuelled cells at all.
        /// </summary>
        public bool IsFullyBurnedInBounds(Bounds worldBounds)
        {
            bool anyFuelCell = false;
            bool allBurned = true;
            ForEachCellInBounds(worldBounds, cell =>
            {
                // Only consider cells that ever had fuel (part of the object).
                if (cell.fuel > 0f || cell.state != FireState.Unburned)
                {
                    anyFuelCell = true;
                    if (cell.state != FireState.Burned) allBurned = false;
                }
            });
            return anyFuelCell && allBurned;
        }

        /// <summary>Run an action over every in-bounds grid cell overlapping a world volume.</summary>
        private void ForEachCellInBounds(Bounds worldBounds, System.Action<FireCell> action)
        {
            Vector3Int min = Grid.WorldToGrid(worldBounds.min);
            Vector3Int max = Grid.WorldToGrid(worldBounds.max);

            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                    {
                        FireCell cell = Grid.GetCell(new Vector3Int(x, y, z));
                        if (cell != null) action(cell);
                    }
        }

        /// <summary>
        /// Run an action over every in-bounds grid cell within a world-space sphere.
        /// The action receives the cell's grid position and a 0..1 falloff (1 at the
        /// centre, 0 at the edge) so callers can taper strength like the water shapes.
        /// </summary>
        private void ForEachCellInSphere(Vector3 worldCenter, float radius,
            System.Action<Vector3Int, float> action)
        {
            if (radius <= 0f) return;
            var min = new Vector3(worldCenter.x - radius, worldCenter.y - radius, worldCenter.z - radius);
            var max = new Vector3(worldCenter.x + radius, worldCenter.y + radius, worldCenter.z + radius);
            Vector3Int gMin = Grid.WorldToGrid(min);
            Vector3Int gMax = Grid.WorldToGrid(max);

            for (int x = gMin.x; x <= gMax.x; x++)
                for (int y = gMin.y; y <= gMax.y; y++)
                    for (int z = gMin.z; z <= gMax.z; z++)
                    {
                        var gp = new Vector3Int(x, y, z);
                        if (Grid.GetCell(gp) == null) continue;

                        float dist = Vector3.Distance(Grid.GridToWorld(gp), worldCenter);
                        if (dist > radius) continue;

                        action(gp, 1f - (dist / radius));
                    }
        }

        /// <summary>Smoke density (0..1) at a grid position, or 0 if out of bounds.</summary>
        public float GetSmoke(Vector3Int gridPos)
        {
            FireCell cell = Grid.GetCell(gridPos);
            return cell != null ? cell.smoke : 0f;
        }

        /// <summary>Smoke density (0..1) at a world-space point.</summary>
        public float GetSmokeWorld(Vector3 worldPos) => GetSmoke(Grid.WorldToGrid(worldPos));

        /// <summary>Average smoke across the whole grid (0..1). A simple "how smoky is it" metric.</summary>
        public float AverageSmoke()
        {
            FireCell[] cells = Grid.Cells;
            if (cells.Length == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < cells.Length; i++) total += cells[i].smoke;
            return total / cells.Length;
        }

        /// <summary>How many cells are currently on fire.</summary>
        public int CountBurning()
        {
            int n = 0;
            FireCell[] cells = Grid.Cells;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state == FireState.Burning) n++;
            return n;
        }

        /// <summary>Fraction of the grid that has burned or is burning (0..1).</summary>
        public float BurnedFraction()
        {
            int n = 0;
            FireCell[] cells = Grid.Cells;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state != FireState.Unburned) n++;
            return cells.Length == 0 ? 0f : (float)n / cells.Length;
        }
    }
}
