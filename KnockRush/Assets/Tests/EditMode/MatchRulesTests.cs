using System;
using KnockRush.GameModes;
using KnockRush.Maps;
using KnockRush.Matchmaking;
using NUnit.Framework;

namespace KnockRush.Tests.EditMode
{
    public sealed class MatchRulesTests
    {
        private static readonly RoundRule[] Classic =
        {
            new RoundRule(MapCategory.Race, 0.625f, 1, false),
            new RoundRule(MapCategory.Survival, 0.5f, 1, false),
            new RoundRule(MapCategory.Survival, 0.5f, 1, false),
            new RoundRule(MapCategory.Final, 1f, 1, true)
        };

        [Test]
        public void ClassicKnockout_32Players_Follows_20_10_5_1()
        {
            var players = 32;
            var expected = new[] { 20, 10, 5, 1 };
            for (var round = 0; round < Classic.Length; round++)
            {
                players = QualificationRules.ComputeQuota(players, Classic[round]);
                Assert.AreEqual(expected[round], players, $"Round {round + 1}");
            }
        }

        [Test]
        public void Quota_AlwaysEliminatesAtLeastOne()
        {
            for (var players = 2; players <= 32; players++)
            {
                foreach (var rule in Classic)
                {
                    var quota = QualificationRules.ComputeQuota(players, rule);
                    Assert.That(quota, Is.InRange(1, players - 1), $"{players} players, ratio {rule.QualifyRatio}");
                }
            }
        }

        [Test]
        public void Quota_SinglePlayerKeepsThePlayer()
        {
            Assert.AreEqual(1, QualificationRules.ComputeQuota(1, Classic[0]));
            Assert.AreEqual(0, QualificationRules.ComputeQuota(0, Classic[0]));
        }

        [Test]
        public void BotFill_FillsUpToTargetButNeverRemovesHumans()
        {
            Assert.AreEqual(32, QualificationRules.ComputeFill(3, 32, 32, true));
            Assert.AreEqual(24, QualificationRules.ComputeFill(3, 32, 24, true));
            Assert.AreEqual(3, QualificationRules.ComputeFill(3, 32, 32, false));
            Assert.AreEqual(30, QualificationRules.ComputeFill(30, 16, 32, true));
        }

        [Test]
        public void RoomCodes_AreValidAndUnambiguous()
        {
            var random = new Random(1234);
            for (var i = 0; i < 500; i++)
            {
                var code = RoomCodes.Generate(random);
                Assert.IsTrue(RoomCodes.IsValid(code), code);
                StringAssert.DoesNotMatch("[01OIL]", code);
            }
        }

        [Test]
        public void RoomCodes_NormalizeUserInput()
        {
            Assert.AreEqual("AB3K7M", RoomCodes.Normalize(" ab3-k7m "));
            Assert.IsTrue(RoomCodes.IsValid(RoomCodes.Normalize("ab3 k7m")));
            Assert.IsFalse(RoomCodes.IsValid("AB0K7M"), "0 is not in the alphabet");
            Assert.IsFalse(RoomCodes.IsValid("ABC"));
            Assert.AreEqual("PR-AB3K7M", RoomCodes.ToSessionName("AB3K7M"));
        }
    }
}
