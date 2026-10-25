using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using FireFight.EditorTools;
using FireFight.Managers;

namespace FireFight.PlayTests
{
    /// <summary>
    /// PlayMode smoke test: build the scene, enter play, wait past the SeedIgniter's one-frame
    /// deferral plus a few simulation seconds, and confirm the demo fire deterministically
    /// started and spread. Proves requirement 2 regardless of Start ordering.
    /// </summary>
    public class IgnitionPlayTests
    {
        private const string ScenePath = "Assets/FireFight/Scenes/IndustrialLayout.unity";

        [UnityTest]
        public IEnumerator DemoFire_IgnitesAndSpreads_OnPlay()
        {
            // Ensure the scene exists and is current (generate if needed, outside play mode).
            if (!System.IO.File.Exists(ScenePath))
            {
                IndustrialLayoutBuilder.BuildBatch();
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();

            // Wait a few seconds of simulation (SeedIgniter fires on frame 2, fire ticks 4/s).
            float t = 0f;
            var mgr = Object.FindFirstObjectByType<FireManager>();
            Assert.IsNotNull(mgr, "No FireManager in the played scene.");

            bool sawBurning = false;
            while (t < 6f)
            {
                if (mgr.CountBurning() > 0) { sawBurning = true; break; }
                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(sawBurning, "Expected the demo fire to ignite within a few seconds on Play.");

            bool anyFlammableBurning = false;
            var flammables = Object.FindObjectsByType<Flammable>(FindObjectsSortMode.None);
            foreach (var fl in flammables)
                if (fl.inflames) { anyFlammableBurning = true; break; }
            Assert.IsTrue(anyFlammableBurning, "Expected at least one Flammable to be on fire.");

            yield return new ExitPlayMode();
        }
    }
}
