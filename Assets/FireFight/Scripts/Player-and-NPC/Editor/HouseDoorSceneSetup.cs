using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FireFight.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace FireFight.Editor
{
    /// <summary>Places doors using the imported house's coordinate system, without moving its art.</summary>
    public static class HouseDoorSceneSetup
    {
        private const string ScenePath = "Assets/FireFight/Scenes/SampleScene.unity";
        private const string HousePath = "Assets/FireFight/HouseFile.obj";
        private const string RootName = "House Doors and Ventilation";
        private const string Request = ".git/house-doors/build.request";
        private const string Result = ".git/house-doors/build.result";

        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.delayCall += ProcessRequest;

        private static void ProcessRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception ex) { File.WriteAllText(Result, ex.ToString()); Debug.LogException(ex); }
        }

        [MenuItem("Tools/FireFight/Set Up House Doors")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before setting up doors.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                if (roots.Any(x => x.name == RootName)) throw new InvalidOperationException("House doors are already set up. Keep the existing doors; this command does not duplicate them.");
                GameObject house = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Select(x => x.gameObject).FirstOrDefault(x => x.name == "HouseFile");
                if (house == null) throw new InvalidOperationException("HouseFile was not found in SampleScene.");
                FireManager manager = roots.SelectMany(x => x.GetComponentsInChildren<FireManager>(true)).Single();
                if (manager.autoDetectOxygenBoxes || (manager.oxygenBoxes != null && manager.oxygenBoxes.Length != 0))
                    throw new InvalidOperationException("Oxygen rooms already exist. Refusing to replace someone else's oxygen setup.");
                var raw = ReadVertices();
                MeshFilter reference = house.GetComponentsInChildren<MeshFilter>(true)
                    .FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.name == "mesh0");
                if (reference == null) reference = house.GetComponentsInChildren<MeshFilter>(true).First();
                Matrix4x4 basis = FindBasis(reference.sharedMesh.vertices, raw);
                Matrix4x4 toWorld = reference.transform.localToWorldMatrix * basis;
                Vector3 up = toWorld.MultiplyVector(Vector3.forward).normalized;
                // Imported CAD axes can point down after the model's scene rotation.
                // Both directions are vertical; choose the lower end of each opening below.
                if (Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.95f)
                    throw new InvalidOperationException("House height axis is tilted too far for vertical doors. Up = " + up);

                var rooms = new[]
                {
                    Room("Main Room", -22.285f, -19.866f, -1.977f, 15.262f, toWorld, manager),
                    Room("Left Rear Room", -22.285f, 15.716f, -12.403f, 22.525f, toWorld, manager),
                    Room("Small Rear Room", -12.403f, 20.481f, -8.572f, 22.561f, toWorld, manager),
                    Room("Right Rear Room", -8.26f, 15.717f, -1.977f, 22.34f, toWorld, manager),
                    Room("Connecting Hall", -12.403f, 15.262f, -8.516f, 20.481f, toWorld, manager)
                };

                Material wood = MaterialAt("HouseDoor_Wood", new Color(0.28f, 0.15f, 0.07f));
                Material metal = MaterialAt("HouseDoor_Hardware", new Color(0.5f, 0.52f, 0.54f));
                GameObject container = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(container, "Add house doors");
                // Door leaves carve at runtime; never bake a permanent hole beneath a closed leaf.
                Type modifierType = Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
                if (modifierType != null)
                {
                    var modifier = container.AddComponent(modifierType);
                    var settings = new SerializedObject(modifier);
                    settings.FindProperty("m_IgnoreFromBuild").boolValue = true;
                    settings.FindProperty("m_ApplyToChildren").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                HouseVentilation ventilation = container.AddComponent<HouseVentilation>();
                var doors = new List<HouseDoor>();
                // Coordinates are the measured openings in the original CAD house, in metres.
                doors.Add(Door("Front Entrance", -18.7881f, -20.0f, -13.1553f, -20.0f, 0, -1, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Side Entrance", -1.85f, -13.397f, -1.85f, -11.199f, 0, -1, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Rear Entrance", -19.590f, 22.70f, -16.835f, 22.70f, 1, -1, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Hallway", -12.8434f, 15.49f, -9.723f, 15.49f, 0, 4, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Left Room", -12.62f, 15.716f, -12.62f, 18.858f, 1, 4, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Small Rear Room", -11.670f, 20.29f, -9.367f, 20.29f, 2, 4, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Right Room", -8.39f, 16.122f, -8.39f, 18.829f, 3, 4, toWorld, container, ventilation, wood, metal));
                doors.Add(Door("Front Alcove", -6.877f, -17.8f, -3.691f, -17.8f, 0, 0, toWorld, container, ventilation, wood, metal));

                Undo.RecordObject(manager, "Configure house oxygen rooms");
                manager.oxygenBoxes = rooms;
                EditorUtility.SetDirty(manager);
                SerializedObject v = new SerializedObject(ventilation);
                v.FindProperty("fireManager").objectReferenceValue = manager;
                v.FindProperty("doors").arraySize = doors.Count;
                for (int i = 0; i < doors.Count; i++) v.FindProperty("doors").GetArrayElementAtIndex(i).objectReferenceValue = doors[i];
                v.FindProperty("roomCount").intValue = manager.oxygenBoxes.Length;
                v.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                string report = "Saved SampleScene: " + doors.Count + " doors; " + manager.oxygenBoxes.Length + " oxygen rooms.\n";
                report += "House world basis: " + toWorld + "\n";
                foreach (var door in doors) report += door.name + ": " + door.transform.position + "\n";
                foreach (var room in manager.oxygenBoxes) report += room.name + ": " + room.min + " .. " + room.max + "\n";
                Directory.CreateDirectory(".git/house-doors");
                File.WriteAllText(Result, report);
                Debug.Log(report, container);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static HouseDoor Door(string name, float x1, float y1, float x2, float y2, int a, int b,
            Matrix4x4 world, GameObject parent, HouseVentilation ventilation, Material wood, Material metal)
        {
            float bottom = world.MultiplyVector(Vector3.forward).y >= 0f ? 1.03f : 3.97f;
            Vector3 start = world.MultiplyPoint3x4(new Vector3(x1, y1, bottom));
            Vector3 end = world.MultiplyPoint3x4(new Vector3(x2, y2, bottom));
            // Keep hinges vertical so gravity and NavMesh carving agree, even with small CAD tilt.
            Vector3 along = Vector3.ProjectOnPlane(end - start, Vector3.up).normalized;
            float width = Vector3.ProjectOnPlane(end - start, Vector3.up).magnitude;
            float height = world.MultiplyVector(Vector3.forward * 2.94f).magnitude;
            GameObject root = new GameObject(name + " Door");
            root.transform.SetParent(parent.transform);
            root.transform.SetPositionAndRotation(start, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up));
            var script = root.AddComponent<HouseDoor>();
            bool doubleDoor = width > 1.65f;
            int count = doubleDoor ? 2 : 1;
            var hinges = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                GameObject hinge = new GameObject(i == 0 ? "Left Hinge" : "Right Hinge");
                hinge.transform.SetParent(root.transform, false);
                hinge.transform.localPosition = new Vector3(i == 0 ? 0.025f : width - 0.025f, 0f, 0f);
                hinges[i] = hinge.transform;
                float leafWidth = width / count - 0.04f;
                float sign = i == 0 ? 1f : -1f;
                GameObject leaf = Cube("Door Leaf", hinge.transform,
                    new Vector3(sign * leafWidth * 0.5f, height * 0.5f, 0f), new Vector3(leafWidth, height, 0.12f), wood, true);
                var obstacle = leaf.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                // Raised panels and a handle on each face keep the door readable from either room.
                foreach (float face in new[] { -1f, 1f })
                {
                    Cube("Inset Panel", hinge.transform, new Vector3(sign * leafWidth * 0.5f, height * 0.62f, face * 0.065f),
                        new Vector3(leafWidth * 0.72f, height * 0.48f, 0.025f), wood, false);
                    Cube("Handle", hinge.transform, new Vector3(sign * (leafWidth - 0.15f), height * 0.46f, face * 0.12f),
                        new Vector3(0.18f, 0.045f, 0.09f), metal, false);
                }
            }
            SerializedObject s = new SerializedObject(script);
            s.FindProperty("hinges").arraySize = count;
            s.FindProperty("openAngles").arraySize = count;
            for (int i = 0; i < count; i++)
            {
                s.FindProperty("hinges").GetArrayElementAtIndex(i).objectReferenceValue = hinges[i];
                s.FindProperty("openAngles").GetArrayElementAtIndex(i).floatValue = i == 0 ? -100f : 100f;
            }
            s.FindProperty("ventilation").objectReferenceValue = ventilation;
            s.FindProperty("roomA").intValue = a;
            s.FindProperty("roomB").intValue = b;
            s.FindProperty("closedCenter").vector3Value = new Vector3(width * 0.5f, height * 0.5f, 0f);
            s.FindProperty("closedSize").vector3Value = new Vector3(width - 0.1f, height, 0.25f);
            s.ApplyModifiedPropertiesWithoutUndo();
            return script;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static FireManager.OxygenBoxDef Room(string name, float x1, float y1, float x2, float y2,
            Matrix4x4 world, FireManager manager)
        {
            Bounds bounds = new Bounds(world.MultiplyPoint3x4(new Vector3(x1, y1, 1.05f)), Vector3.zero);
            foreach (float x in new[] { x1, x2 }) foreach (float y in new[] { y1, y2 }) foreach (float z in new[] { 1.05f, 3.95f })
                bounds.Encapsulate(world.MultiplyPoint3x4(new Vector3(x, y, z)));
            var grid = new FireFight.Environment.FireGrid(manager.gridSize, manager.cellSize, manager.transform.position);
            Vector3Int min = Vector3Int.Max(Vector3Int.zero, grid.WorldToGrid(bounds.min));
            Vector3Int max = Vector3Int.Min(manager.gridSize - Vector3Int.one, grid.WorldToGrid(bounds.max));
            if (min.x > max.x || min.y > max.y || min.z > max.z) throw new InvalidOperationException(name + " is outside the fire grid.");
            return new FireManager.OxygenBoxDef { name = name, min = min, max = max, startOpen = false };
        }

        private static Material MaterialAt(string name, Color color)
        {
            string folder = "Assets/FireFight/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/FireFight", "Materials");
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", name.Contains("Hardware") ? 0.65f : 0.25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static List<Vector3> ReadVertices()
        {
            var result = new List<Vector3>();
            foreach (string line in File.ReadLines(HousePath))
            {
                if (!line.StartsWith("v ")) continue;
                string[] f = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                result.Add(new Vector3(float.Parse(f[1], CultureInfo.InvariantCulture),
                    float.Parse(f[2], CultureInfo.InvariantCulture), float.Parse(f[3], CultureInfo.InvariantCulture)));
            }
            return result;
        }

        private static Matrix4x4 FindBasis(Vector3[] mesh, List<Vector3> raw)
        {
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            Matrix4x4 best = Matrix4x4.identity;
            int score = -1;
            foreach (int x in new[] { 0, 1, 2 }) foreach (int y in new[] { 0, 1, 2 }) foreach (int z in new[] { 0, 1, 2 })
            {
                if (x == y || x == z || y == z) continue;
                foreach (int sx in new[] { -1, 1 }) foreach (int sy in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                {
                    Matrix4x4 candidate = Matrix4x4.identity;
                    candidate.SetColumn(0, axes[x] * sx);
                    candidate.SetColumn(1, axes[y] * sy);
                    candidate.SetColumn(2, axes[z] * sz);
                    int matches = 0;
                    foreach (var point in mesh.Take(30))
                        if (raw.Any(p => (candidate.MultiplyPoint3x4(p) - point).sqrMagnitude < 0.00001f)) matches++;
                    if (matches > score) { best = candidate; score = matches; }
                }
            }
            if (score < Mathf.Min(mesh.Length, 30) * 0.9f)
                throw new InvalidOperationException("Could not calibrate the imported house mesh. Matched " + score + " vertices.");
            return best;
        }
    }
}
