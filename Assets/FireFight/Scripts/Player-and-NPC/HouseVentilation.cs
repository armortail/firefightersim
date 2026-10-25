using UnityEngine;
using FireFight.Managers;

namespace FireFight
{
    /// <summary>Connects house door states to the existing oxygen pools, preserving their oxygen.</summary>
    [DisallowMultipleComponent]
    public class HouseVentilation : MonoBehaviour
    {
        [SerializeField] private FireManager fireManager;
        [SerializeField] private HouseDoor[] doors;
        [SerializeField] private int firstRoomIndex;
        [SerializeField] private int roomCount = 5;
        private bool[] vented;

        private void Start()
        {
            if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
            if (fireManager == null || fireManager.autoDetectOxygenBoxes ||
                fireManager.OxygenBoxCount < firstRoomIndex + roomCount)
            {
                Debug.LogError("House ventilation needs its authored oxygen rooms on FireManager.", this);
                enabled = false;
                return;
            }
            RefreshVentilation();
        }

        public float GetRoomOxygen(int room) => fireManager != null && room >= 0 && room < roomCount
            ? fireManager.GetOxygen(firstRoomIndex + room) : -1f;

        public void RefreshVentilation()
        {
            if (fireManager == null || fireManager.Grid == null) return;
            if (vented == null || vented.Length != roomCount) vented = new bool[roomCount];
            System.Array.Clear(vented, 0, vented.Length);
            // First vent rooms with an open exterior door, then follow open interior doors.
            foreach (var door in doors)
                if (door != null && door.IsOpen && door.RoomB < 0 && door.RoomA >= 0 && door.RoomA < roomCount)
                    vented[door.RoomA] = true;
            for (int pass = 0; pass < roomCount; pass++)
                foreach (var door in doors)
                {
                    if (door == null || !door.IsOpen || door.RoomA < 0 || door.RoomB < 0 ||
                        door.RoomA >= roomCount || door.RoomB >= roomCount) continue;
                    if (vented[door.RoomA] || vented[door.RoomB])
                        vented[door.RoomA] = vented[door.RoomB] = true;
                }
            for (int i = 0; i < roomCount; i++) fireManager.SetOxygenBoxOpen(firstRoomIndex + i, vented[i]);
        }
    }
}
