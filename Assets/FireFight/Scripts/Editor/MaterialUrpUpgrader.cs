using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;          // MaterialUpgrader (base class)
using UnityEditor.Rendering.Universal; // StandardUpgrader, ParticleUpgrader
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireFight.EditorTools
{
    /// <summary>
    /// One-click fix for "pink" materials. Imported asset packs ship materials that
    /// use the built-in (Standard/Legacy) shaders, which render magenta under URP.
    /// This converts every selected or project material that is still on a built-in
    /// shader over to the correct Universal Render Pipeline shader, remapping the
    /// albedo / metallic / normal / emission texture slots via Unity's official
    /// URP material upgrader (so textures survive the switch).
    ///
    /// Menu: Tools > FireFight > Fix Pink Materials (Convert to URP)
    /// </summary>
    public static class MaterialUrpUpgrader
    {
        private const string MenuRoot = "Tools/FireFight/";

        [MenuItem(MenuRoot + "Fix Pink Materials (Convert ALL to URP)")]
        public static void ConvertAll()
        {
            Convert(FindAllProjectMaterials(), "entire project");
        }

        [MenuItem(MenuRoot + "Fix Pink Materials (Convert SELECTED to URP)")]
        public static void ConvertSelected()
        {
            var mats = new List<Material>();
            foreach (var obj in Selection.objects)
            {
                if (obj is Material m) mats.Add(m);
            }
            if (mats.Count == 0)
            {
                EditorUtility.DisplayDialog("Fix Pink Materials",
                    "No materials selected. Select material assets in the Project window, " +
                    "or use 'Convert ALL to URP'.", "OK");
                return;
            }
            Convert(mats, "selection");
        }

        /// <summary>
        /// Programmatic, dialog-free conversion used by other editor tools (e.g. the
        /// Industrial Layout builder). Converts any built-in materials in the list to URP.
        /// Returns the number of materials actually converted.
        /// </summary>
        public static int ConvertForBuilder(List<Material> materials)
        {
            if (materials == null) return 0;
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
                return 0;

            var toConvert = new List<Material>();
            foreach (var m in materials)
            {
                if (m == null || m.shader == null) continue;
                if (IsBuiltInShader(m.shader)) toConvert.Add(m);
            }
            if (toConvert.Count == 0) return 0;

            var standard = new StandardUpgrader("Standard");
            var standardSpec = new StandardUpgrader("Standard (Specular setup)");
            var particles = new ParticleUpgrader("Legacy Shaders/Particles/Alpha Blended");

            int converted = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var m in toConvert)
                {
                    string shaderName = m.shader != null ? m.shader.name : "";
                    MaterialUpgrader upgrader = shaderName switch
                    {
                        "Standard (Specular setup)" => standardSpec,
                        var s when s.StartsWith("Legacy Shaders/Particles") || s.StartsWith("Particles/") => particles,
                        _ => standard,
                    };
                    upgrader.Upgrade(m, MaterialUpgrader.UpgradeFlags.None);
                    EditorUtility.SetDirty(m);
                    converted++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Debug.Log($"[FireFight] URP material upgrade (builder): {converted} material(s) converted.");
            return converted;
        }

        private static void Convert(List<Material> materials, string scopeLabel)
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
            {
                EditorUtility.DisplayDialog("Fix Pink Materials",
                    "The active render pipeline is not URP. Aborting so nothing is damaged.", "OK");
                return;
            }

            // Only touch materials that are actually on a non-URP (built-in) shader.
            var toConvert = new List<Material>();
            foreach (var m in materials)
            {
                if (m == null || m.shader == null) continue;
                if (IsBuiltInShader(m.shader)) toConvert.Add(m);
            }

            if (toConvert.Count == 0)
            {
                EditorUtility.DisplayDialog("Fix Pink Materials",
                    $"Scanned {materials.Count} material(s) in the {scopeLabel}; " +
                    "none were on a built-in shader. Nothing to convert.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Fix Pink Materials",
                    $"Convert {toConvert.Count} built-in material(s) in the {scopeLabel} " +
                    "to Universal Render Pipeline/Lit?\n\nThis rewrites each material's shader " +
                    "and remaps its texture slots. Make sure your work is committed first.",
                    "Convert", "Cancel"))
            {
                return;
            }

            var standard = new StandardUpgrader("Standard");
            var standardSpec = new StandardUpgrader("Standard (Specular setup)");
            var particles = new ParticleUpgrader("Legacy Shaders/Particles/Alpha Blended");

            int converted = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var m in toConvert)
                {
                    string shaderName = m.shader != null ? m.shader.name : "";
                    MaterialUpgrader upgrader = shaderName switch
                    {
                        "Standard (Specular setup)" => standardSpec,
                        var s when s.StartsWith("Legacy Shaders/Particles") || s.StartsWith("Particles/") => particles,
                        _ => standard,
                    };
                    upgrader.Upgrade(m, MaterialUpgrader.UpgradeFlags.None);
                    EditorUtility.SetDirty(m);
                    converted++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[FireFight] URP material upgrade complete: {converted} material(s) converted.");
            EditorUtility.DisplayDialog("Fix Pink Materials",
                $"Done. Converted {converted} material(s) to URP.\n\n" +
                "If any material still looks wrong, re-assign its base map texture in the Inspector.",
                "OK");
        }

        private static bool IsBuiltInShader(Shader shader)
        {
            // Built-in shaders live in the engine's "unity default resources" / "Resources/unity_builtin_extra".
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith("Resources/") || path.Contains("unity_builtin_extra") ||
                path.Contains("unity default resources"))
            {
                return true;
            }
            // Fallback: treat classic Standard / Legacy / Mobile shaders as built-in.
            string n = shader.name;
            return n == "Standard" || n == "Standard (Specular setup)" ||
                   n.StartsWith("Legacy Shaders/") || n.StartsWith("Mobile/");
        }

        private static List<Material> FindAllProjectMaterials()
        {
            var result = new List<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Skip package materials; only touch the project's own Assets.
                if (!path.StartsWith("Assets/")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) result.Add(m);
            }
            return result;
        }
    }
}
