using KnockRush.Movement;
using NUnit.Framework;
using UnityEngine;

namespace KnockRush.Tests.EditMode
{
    public sealed class MovementSettingsTests
    {
        [Test]
        public void JumpVelocityMatchesJumpHeight()
        {
            var settings = ScriptableObject.CreateInstance<MovementSettings>();
            try
            {
                var expected = Mathf.Sqrt(2f * settings.Gravity * settings.JumpHeight);
                Assert.AreEqual(expected, settings.JumpVelocity, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DefaultsAreSane()
        {
            var settings = ScriptableObject.CreateInstance<MovementSettings>();
            try
            {
                Assert.Greater(settings.Height, settings.Radius * 2f);
                Assert.Greater(settings.DiveHorizontalSpeed, settings.MaxSpeed);
                Assert.GreaterOrEqual(settings.SlideMaxDuration, settings.SlideMinDuration);
                Assert.That(settings.MinGroundNormalY, Is.InRange(0f, 1f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
