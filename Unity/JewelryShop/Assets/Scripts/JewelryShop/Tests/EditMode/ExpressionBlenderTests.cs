using System.Collections.Generic;
using System.Linq;
using JewelryShop.Core;
using NUnit.Framework;

namespace JewelryShop.Tests
{
    [TestFixture]
    public class ExpressionBlenderTests
    {
        private const string Wave = "Base Layer.Wave";
        private const string Bow = "Base Layer.Bow";
        private const string Shake = "Base Layer.Shake";
        private const string Shy = "Base Layer.Embarrassed";

        private static ExpressionBlender NewBlender(float fade = 0.3f)
        {
            var s = ExpressionSettings.Defaults;
            s.FadeSeconds = fade;
            return new ExpressionBlender(s);
        }

        // Starts a gesture and plays it to the end of its fade-in.
        private static ExpressionWeights Play(ExpressionBlender b, string gesture)
        {
            b.StartGesture(gesture);
            return b.Tick(1f, true);
        }

        [Test]
        public void NoGesture_ShowsDefaultSmile()
        {
            var w = NewBlender().Tick(1f, false);
            Assert.That(w.Happy, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(w.Relaxed + w.Angry + w.Sad, Is.EqualTo(0f));
        }

        [Test]
        public void Gesture_ShowsItsExpression()
        {
            Assert.That(Play(NewBlender(), Wave).Happy, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(Play(NewBlender(), Bow).Relaxed, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(Play(NewBlender(), Shake).Sad, Is.EqualTo(0.3f).Within(1e-5f));
            var shy = Play(NewBlender(), Shy);
            Assert.That(shy.Happy, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(shy.Relaxed, Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void Gesture_IsFullAtStart_WhenFadeIsZero()
        {
            var b = NewBlender(0f);
            b.StartGesture(Wave);
            Assert.That(b.Current.Happy, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void AfterGestureEnds_HoldsForHoldSeconds()
        {
            var b = NewBlender();
            Play(b, Wave);
            var w = b.Tick(0.49f, false);
            Assert.That(w.Happy, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void AfterHold_FadesToDefaultSmileOverFadeSeconds()
        {
            var b = NewBlender();
            Play(b, Wave);
            b.Tick(0.5f, false);
            var half = b.Tick(0.15f, false);
            Assert.That(half.Happy, Is.EqualTo(0.55f).Within(1e-4f));
            var done = b.Tick(0.15f, false);
            Assert.That(done.Happy, Is.EqualTo(0.3f).Within(1e-4f));
        }

        [Test]
        public void ActiveGesture_KeepsHoldFromRunningDown()
        {
            var b = NewBlender();
            Play(b, Wave);
            var w = b.Tick(5f, true);
            Assert.That(w.Happy, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void SecondGesture_MidHold_Crossfades()
        {
            var b = NewBlender();
            Play(b, Wave);
            b.StartGesture(Bow);
            var half = b.Tick(0.15f, true);
            Assert.That(half.Happy, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(half.Relaxed, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(MaxWeight(half), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void SecondGesture_MidFade_StartsFromCurrentWeights()
        {
            var b = NewBlender();
            Play(b, Wave);
            b.Tick(0.5f, false);
            var mid = b.Tick(0.15f, false); // happy 0.55
            b.StartGesture(Shake);
            Assert.That(b.Current.Happy, Is.EqualTo(mid.Happy).Within(1e-5f));
            var after = b.Tick(0.15f, true);
            Assert.That(after.Happy, Is.EqualTo(0.275f).Within(1e-4f));
            Assert.That(after.Sad, Is.EqualTo(0.15f).Within(1e-4f));
        }

        [Test]
        public void RepeatingGesture_RestartsHold()
        {
            var b = NewBlender();
            Play(b, Wave);
            b.Tick(0.4f, false);
            b.StartGesture(Wave);
            var w = b.Tick(0.4f, false);
            Assert.That(w.Happy, Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void UnmappedGesture_ShowsOnlyDefaultSmile()
        {
            var w = Play(NewBlender(), "Base Layer.Dance");
            Assert.That(w.Happy, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(w.Relaxed + w.Angry + w.Sad, Is.EqualTo(0f));
        }

        [Test]
        public void ZeroFade_SnapsWithoutDividingByZero()
        {
            var b = NewBlender(0f);
            Play(b, Wave);
            b.Tick(0.5f, false);
            var w = b.Tick(0.01f, false);
            Assert.That(w.Happy, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(float.IsNaN(w.Happy), Is.False);
        }

        [Test]
        public void Validation_ClampsWeightsAndTimes_WithWarnings()
        {
            var s = ExpressionSettings.Defaults;
            s.Entries = new[]
            {
                new ExpressionEntry { StateName = Wave, Happy = 2f, Sad = -1f },
            };
            s.HoldSeconds = -1f;
            s.FadeSeconds = -1f;
            s.DefaultSmileWeight = 5f;
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.Entries[0].Happy, Is.EqualTo(1f));
            Assert.That(v.Entries[0].Sad, Is.EqualTo(0f));
            Assert.That(v.HoldSeconds, Is.EqualTo(0f));
            Assert.That(v.FadeSeconds, Is.EqualTo(0f));
            Assert.That(v.DefaultSmileWeight, Is.EqualTo(1f));
            Assert.That(warnings.Count, Is.EqualTo(5));
        }

        [Test]
        public void Validation_DropsDuplicateAndEmptyEntries()
        {
            var s = ExpressionSettings.Defaults;
            s.Entries = new[]
            {
                new ExpressionEntry { StateName = Wave, Happy = 0.5f },
                new ExpressionEntry { StateName = Wave, Happy = 0.9f },
                new ExpressionEntry { StateName = " ", Happy = 0.9f },
            };
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.Entries.Length, Is.EqualTo(1));
            Assert.That(v.Entries[0].Happy, Is.EqualTo(0.5f));
            Assert.That(warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Validation_EmptyTable_ResetsToDefaults()
        {
            var s = ExpressionSettings.Defaults;
            s.Entries = new ExpressionEntry[0];
            var warnings = new List<string>();
            var v = s.Validated(warnings);
            Assert.That(v.Entries.Length, Is.EqualTo(ExpressionSettings.Defaults.Entries.Length));
            Assert.That(warnings, Is.Not.Empty);
        }

        [Test]
        public void Defaults_AreValidWithoutWarnings()
        {
            var warnings = new List<string>();
            ExpressionSettings.Defaults.Validated(warnings);
            Assert.That(warnings, Is.Empty);
        }

        private static float MaxWeight(ExpressionWeights w) =>
            new[] { w.Happy, w.Relaxed, w.Angry, w.Sad }.Max();
    }
}
