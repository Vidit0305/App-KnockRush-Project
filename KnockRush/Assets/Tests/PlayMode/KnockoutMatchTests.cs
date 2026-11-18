using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using KnockRush.GameModes;
using KnockRush.Maps;
using KnockRush.Multiplayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// End-to-end knockout match on a real Fusion runner (Single mode) with server bots:
    /// lobby → bot fill → round load → countdown → race → qualification → results.
    /// </summary>
    public sealed class KnockoutMatchTests
    {
        private const float Timeout = 40f;

        [UnityTearDown]
        public IEnumerator Stop()
        {
            if (NetworkSession.Current != null)
            {
                var stop = NetworkSession.Current.StopAsync();
                while (!stop.IsCompleted)
                {
                    yield return null;
                }

                var elapsed = 0f;
                while (NetworkSession.Current != null && elapsed < 10f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator QuickRace_WithBots_LocalFinisherQualifiesAndMatchEnds()
        {
            var start = NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Single,
                SessionName = "test-quickrace",
                GameModeId = "quickrace",
                FillWithBots = true,
                LobbySeconds = 1f,
                RoundTimeLimitOverride = 12f
            });
            yield return Wait(start);
            Assert.IsTrue(start.Result.Ok, start.Result.ShutdownReason.ToString());

            // Lobby → match: bots fill the session and the round loads.
            yield return WaitUntil(() => MatchController.Current != null && MatchController.Current.Phase == MatchPhase.Countdown, "countdown");
            var match = MatchController.Current;
            Assert.Greater(match.PlayerCount(), 1, "Bots should fill the match.");
            Assert.AreEqual(match.PlayerCount(), match.ActiveCount());
            var expectedQuota = QualificationRules.ComputeQuota(match.PlayerCount(), match.Mode.Rounds[0]);
            Assert.AreEqual(expectedQuota, match.Quota);
            Assert.AreEqual(match.PlayerCount(), Object.FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None).Length);

            yield return WaitUntil(() => match.Phase == MatchPhase.Playing, "playing");

            // Send the local human across the finish line.
            var localSlot = match.LocalSlot();
            Assert.GreaterOrEqual(localSlot, 0);
            var local = Object.FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None).First(c => c.HasInputAuthority);
            var finish = Object.FindFirstObjectByType<FinishLine>();
            local.transform.position = finish.transform.position;
            yield return WaitUntil(() => match.Records.Get(localSlot).Status == RecordStatus.Qualified, "local qualification");
            Assert.AreEqual(1, match.Records.Get(localSlot).RoundPlacement, "First finisher should be #1.");

            // Round ends (quota or time limit), then match results with the local player as winner.
            yield return WaitUntil(() => match.Phase == MatchPhase.MatchResults, "match results");
            Assert.AreEqual(RecordStatus.Qualified, match.Records.Get(localSlot).Status);
            Assert.LessOrEqual(match.QualifiedCount(), match.Quota, "No more than the quota may qualify in a race.");
            Assert.AreEqual(localSlot, match.WinnerSlot, "Best placed qualifier is the winner.");
        }

        private static IEnumerator Wait(Task task)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < Timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "Task timed out");
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            var elapsed = 0f;
            while (!condition() && elapsed < Timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(condition(), $"Timed out waiting for {what}. Phase: {(MatchController.Current != null ? MatchController.Current.Phase.ToString() : "no match")}");
        }
    }
}
