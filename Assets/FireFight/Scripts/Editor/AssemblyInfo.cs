using System.Runtime.CompilerServices;

// The Editor-only test assembly reaches the builder's internal pure helpers
// (IndustrialLayoutBuilder.ComputeFireGrid / PickCentralPrefab / CLEARANCE / MAX_CELLS)
// for unit testing. Keeping those helpers internal avoids widening the public API.
[assembly: InternalsVisibleTo("FireFight.Tests")]
