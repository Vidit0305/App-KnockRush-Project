using System.Collections;
using System.Linq;
using KnockRush.Core;
using KnockRush.Maps;
using KnockRush.Obstacles;
using KnockRush.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>Structural checks every map must pass (offline load).</summary>
    public sealed class MapIntegrityTests
    {
        public static readonly string[] AllMaps =
        {
            "HammerRush", "SlimeMountain", "SkyHigh", "SpinAttack", "FallingTiles", "PendulumPanic", "CrownClimber", "LastPlatform"
        };

        [UnityTest]
        public IEnumerator MapIsPlayable([ValueSource(nameof(AllMaps))] string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return null;

            var map = MapController.Current;
            Assert.IsNotNull(map, "MapController missing");
            Assert.IsNotNull(map.Definition, "MapDefinition missing");
            Assert.AreEqual(sceneName, map.Definition.SceneName);
            Assert.GreaterOrEqual(map.SpawnPointCount, 32, "Needs 32 spawn points");

            for (var i = 0; i < map.SpawnPointCount; i++)
            {
                var spawn = map.GetSpawnPoint(i).position;
                Assert.IsTrue(Physics.Raycast(spawn + Vector3.up, Vector3.down, out _, 3f, GameLayers.EnvironmentMask),
                    $"Spawn {i} at {spawn} has no ground");
            }

            var finish = Object.FindFirstObjectByType<FinishLine>();
            var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).OrderBy(c => c.Order).ToArray();
            var isRaceLike = map.Definition.FallRule == FallRule.RespawnAtCheckpoint;
            if (isRaceLike)
            {
                Assert.IsNotNull(finish, "Race maps need a finish line");
                Assert.GreaterOrEqual(checkpoints.Length, 1, "Race maps need checkpoints");
                for (var i = 0; i < checkpoints.Length; i++)
                {
                    Assert.AreEqual(i + 1, checkpoints[i].Order, "Checkpoint orders must be 1..N");
                }
            }
            else
            {
                Assert.IsNull(finish, "Survival maps have no finish line");
            }

            Assert.Greater(ObstacleSimulation.Count + Object.FindObjectsByType<FallingTile>(FindObjectsSortMode.None).Length, 0, "Map has no obstacles");
            Assert.IsNotNull(Object.FindFirstObjectByType<KillVolume>(), "Map needs a kill volume");
            Assert.IsNotNull(Object.FindFirstObjectByType<PlayerCharacter>(), "Offline rig did not spawn a player");

            // The offline player must still be on the map after a moment (sweepers may knock it about,
            // so "grounded" is not guaranteed at any given instant).
            yield return new WaitForSeconds(1.5f);
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            Assert.Greater(player.Position.y, map.KillHeight + 2f, $"Player fell out at {player.Position} on {sceneName}");
        }
    }
}
