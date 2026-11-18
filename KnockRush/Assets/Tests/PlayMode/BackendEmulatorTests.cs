using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnockRush.Backend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// Runs the real Unity backend client against the Firebase Emulator Suite. Skipped automatically
    /// when the emulators are not running. Run with:
    ///   cd Backend/firebase/functions && npx firebase emulators:exec --project demo-knockrush
    ///      --only auth,firestore,functions "bash ../../../Tools/unity.sh playmode"
    /// </summary>
    public sealed class BackendEmulatorTests
    {
        private const float Timeout = 30f;

        [UnityTest]
        public IEnumerator GuestSignIn_Profile_Shop_Daily_Equip()
        {
            yield return RequireEmulator();

            PlayerPrefs.DeleteKey("knockrush.auth.refresh");
            PlayerPrefs.DeleteKey("knockrush.auth.uid");
            var config = ScriptableObject.CreateInstance<BackendConfig>();
            config.ConfigureEmulator("127.0.0.1", false);
            var backend = new BackendService(config);

            var signIn = backend.SignInAsync();
            yield return Wait(signIn);
            var profile = signIn.Result;
            Assert.AreEqual(BackendState.Online, backend.State);
            Assert.AreEqual(500, profile.Coins);
            Assert.AreEqual(1, profile.Level);
            Assert.IsTrue(profile.Owns("default_bean"));
            Assert.IsFalse(string.IsNullOrEmpty(backend.Uid));

            var catalog = backend.GetCatalogAsync();
            yield return Wait(catalog);
            Assert.IsTrue(catalog.Result.Exists(item => item.Id == "color_purple"));

            var purchase = backend.PurchaseAsync("color_purple");
            yield return Wait(purchase);
            Assert.AreEqual(200, backend.Profile.Coins);
            Assert.IsTrue(backend.Profile.Owns("color_purple"));

            var daily = backend.ClaimDailyRewardAsync();
            yield return Wait(daily);
            Assert.AreEqual(1, daily.Result.Streak);
            Assert.AreEqual(250, backend.Profile.Coins);

            var equip = backend.EquipAsync(new Dictionary<string, string> { ["color"] = "color_purple" });
            yield return Wait(equip);
            Assert.AreEqual("color_purple", backend.Profile.Equipped["color"]);

            // Server rejects buying something unaffordable.
            var expensive = backend.PurchaseAsync("body_dino");
            yield return WaitAllowFault(expensive);
            Assert.IsTrue(expensive.IsFaulted);
            Assert.AreEqual("FAILED_PRECONDITION", ((BackendException)expensive.Exception.InnerException).Status);

            // Session persists: a new client instance restores the same account.
            var again = new BackendService(config);
            var restore = again.SignInAsync();
            yield return Wait(restore);
            Assert.AreEqual(backend.Uid, again.Uid);
            Assert.AreEqual(250, restore.Result.Coins);
        }

        private static IEnumerator RequireEmulator()
        {
            using (var probe = UnityWebRequest.Get("http://127.0.0.1:9099/"))
            {
                probe.timeout = 2;
                yield return probe.SendWebRequest();
                if (probe.result == UnityWebRequest.Result.ConnectionError)
                {
                    Assert.Ignore("Firebase emulators not running; skipped.");
                }
            }
        }

        private static IEnumerator Wait(Task task)
        {
            yield return WaitAllowFault(task);
            if (task.IsFaulted)
            {
                throw task.Exception;
            }
        }

        private static IEnumerator WaitAllowFault(Task task)
        {
            var elapsed = 0f;
            while (!task.IsCompleted && elapsed < Timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(task.IsCompleted, "Backend call timed out.");
        }
    }
}
