using System.Collections;
using System.Linq;
using KnockRush.Core;
using KnockRush.Core.Events;
using KnockRush.Core.SceneManagement;
using KnockRush.Maps;
using KnockRush.Obstacles;
using KnockRush.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>Integration tests for the first playable map (structure + offline race flow).</summary>
    public sealed class HammerRushTests
    {
        private PlayerCharacter player;

        [UnitySetUp]
        public IEnumerator LoadMap()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.HammerRush, LoadSceneMode.Single);
            yield return null;
            player = Object.FindFirstObjectByType<PlayerCharacter>();
            Assert.IsNotNull(player, "Map has no player.");
        }

        [Test]
        public void MapIsWiredForA32PlayerRace()
        {
            var map = MapController.Current;
            Assert.IsNotNull(map);
            Assert.AreEqual(MapCategory.Race, map.Definition.Category);
            Assert.GreaterOrEqual(map.SpawnPointCount, 32);

            for (var i = 0; i < map.SpawnPointCount; i++)
            {
                var spawn = map.GetSpawnPoint(i).position;
                Assert.IsTrue(Physics.Raycast(spawn + Vector3.up, Vector3.down, 3f, GameLayers.EnvironmentMask),
                    $"Spawn {i} at {spawn} has no ground.");
            }

            var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).OrderBy(c => c.Order).ToArray();
            Assert.AreEqual(5, checkpoints.Length);
            for (var i = 1; i < checkpoints.Length; i++)
            {
                Assert.Greater(checkpoints[i].transform.position.z, checkpoints[i - 1].transform.position.z, "Checkpoints out of course order.");
            }

            var finish = Object.FindFirstObjectByType<FinishLine>();
            Assert.IsNotNull(finish);
            Assert.Greater(finish.transform.position.z, checkpoints.Last().transform.position.z);
            Assert.GreaterOrEqual(ObstacleSimulation.Count, 12, "Expected hammers, sweepers, platforms and windmill.");
        }

        [UnityTest]
        public IEnumerator CountdownLocksInputThenRaceStarts()
        {
            var session = OfflineRaceSession.Current;
            Assert.IsNotNull(session);
            Assert.AreEqual(OfflineRaceSession.Phase.Countdown, session.CurrentPhase);
            Assert.IsTrue(player.InputLocked);

            yield return new WaitForSeconds(3.5f);

            Assert.AreEqual(OfflineRaceSession.Phase.Running, session.CurrentPhase);
            Assert.IsFalse(player.InputLocked);
        }

        [UnityTest]
        public IEnumerator CheckpointThenFallRespawnsAtCheckpoint()
        {
            var checkpoint = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).First(c => c.Order == 2);
            player.Teleport(checkpoint.transform.position);
            for (var i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(2, player.CheckpointOrder);

            player.Teleport(new Vector3(0f, -30f, 60f)); // below the kill height
            for (var i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(checkpoint.transform.position.z, player.Position.z, 1f, "Did not respawn at checkpoint 2.");
            Assert.Greater(player.Position.y, -2f);
        }

        [UnityTest]
        public IEnumerator SwingingHammerKnocksStandingPlayer()
        {
            var knocked = false;
            void OnEvents(KnockRush.Movement.MotorEvents events) => knocked |= (events & KnockRush.Movement.MotorEvents.Impulse) != 0;
            player.MotorEventsRaised += OnEvents;
            try
            {
                player.Teleport(new Vector3(0f, 0.2f, 16f)); // under Hammer_0's swing
                var elapsed = 0f;
                while (!knocked && elapsed < 4f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                Assert.IsTrue(knocked, "The hammer never hit the player within one full swing.");
            }
            finally
            {
                player.MotorEventsRaised -= OnEvents;
            }
        }

        [UnityTest]
        public IEnumerator CrossingFinishLineQualifies()
        {
            RaceFinishedEvent? finished = null;
            void OnFinished(RaceFinishedEvent e) => finished = e;
            EventBus.Subscribe<RaceFinishedEvent>(OnFinished);
            try
            {
                yield return new WaitForSeconds(3.5f); // countdown
                var finish = Object.FindFirstObjectByType<FinishLine>();
                player.Teleport(finish.transform.position);
                for (var i = 0; i < 10 && finished == null; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.IsTrue(finished.HasValue, "RaceFinishedEvent not raised.");
                Assert.IsTrue(finished.Value.Qualified);
                Assert.AreEqual(1, finished.Value.Placement);
                Assert.IsTrue(player.HasFinished);
                Assert.IsTrue(player.InputLocked);
            }
            finally
            {
                EventBus.Unsubscribe<RaceFinishedEvent>(OnFinished);
            }
        }
    }
}
