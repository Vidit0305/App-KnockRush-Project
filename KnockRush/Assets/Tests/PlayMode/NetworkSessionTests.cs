using System.Collections;
using System.Threading.Tasks;
using KnockRush.CameraRig;
using KnockRush.Core;
using KnockRush.Movement;
using KnockRush.Multiplayer;
using KnockRush.Obstacles;
using KnockRush.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// Runs a real Fusion NetworkRunner in Single mode (local, no Photon cloud) on Hammer Rush:
    /// spawning, networked motor simulation, input-authority binding, online rig and
    /// network-driven obstacles are exercised exactly as in multiplayer.
    /// </summary>
    public sealed class NetworkSessionTests
    {
        private const float Timeout = 15f;

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            if (NetworkSession.Current != null)
            {
                var stop = NetworkSession.Current.StopAsync();
                yield return WaitFor(stop);
                var elapsed = 0f;
                while (NetworkSession.Current != null && elapsed < Timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator SingleModeSession_SpawnsAndSimulatesNetworkCharacter()
        {
            var start = NetworkSession.StartAsync(new NetworkLaunchOptions { Role = SessionRole.Single, SessionName = "test-single" });
            yield return WaitFor(start);
            Assert.IsTrue(start.Result.Ok, $"StartGame failed: {start.Result.ShutdownReason}");
            Assert.IsTrue(GameSession.IsOnline);

            NetworkCharacter character = null;
            var elapsed = 0f;
            while (character == null && elapsed < Timeout)
            {
                character = Object.FindFirstObjectByType<NetworkCharacter>();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsNotNull(character, "Server never spawned a networked character.");
            Assert.IsTrue(character.HasStateAuthority);
            Assert.IsTrue(character.HasInputAuthority);
            Assert.IsNull(Object.FindFirstObjectByType<PlayerCharacter>(), "Offline rig must not spawn in online mode.");

            var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(camera, "Online rig camera missing.");
            Assert.AreEqual(character.transform, camera.Target, "Camera not bound to the local networked character.");

            // Let the character fall onto the start platform through networked simulation.
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(character.State.IsGrounded, $"Not grounded: {character.State.Mode} at {character.Position}");
            Assert.That(character.Position.y, Is.InRange(-0.1f, 0.2f));
            Assert.IsTrue(ObstacleSimulation.ExternallyDriven, "Obstacles must follow the network tick.");
        }

        [UnityTest]
        public IEnumerator NetworkCharacter_RespawnsAfterFallingOut()
        {
            var start = NetworkSession.StartAsync(new NetworkLaunchOptions { Role = SessionRole.Single, SessionName = "test-fall" });
            yield return WaitFor(start);
            Assert.IsTrue(start.Result.Ok);

            NetworkCharacter character = null;
            var elapsed = 0f;
            while (character == null && elapsed < Timeout)
            {
                character = Object.FindFirstObjectByType<NetworkCharacter>();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1f);
            var spawn = character.RespawnPosition;
            character.transform.position = new Vector3(0f, -40f, 50f);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(spawn.z, character.Position.z, 1.5f, "Did not respawn at the respawn point.");
            Assert.Greater(character.Position.y, -2f);
        }

        [Test]
        public void InputEncoding_RoundTripsPressesAndClampsMove()
        {
            var first = NetworkInputData.FromMotorInput(new MotorInput { Move = new Vector2(3f, 4f), JumpPressed = true, JumpHeld = true });
            Assert.AreEqual(1f, first.Move.magnitude, 0.001f, "Move must be clamped to unit length.");

            var decoded = first.ToMotorInput(default);
            Assert.IsTrue(decoded.JumpPressed, "Edge should register as a press.");
            Assert.IsTrue(decoded.JumpHeld);

            var held = NetworkInputData.FromMotorInput(new MotorInput { JumpHeld = true });
            var decodedHeld = held.ToMotorInput(first.Buttons);
            Assert.IsFalse(decodedHeld.JumpPressed, "Holding must not re-trigger a press.");
            Assert.IsTrue(decodedHeld.JumpHeld);
        }

        private static IEnumerator WaitFor(Task task)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < Timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "Task timed out.");
            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }
    }
}
