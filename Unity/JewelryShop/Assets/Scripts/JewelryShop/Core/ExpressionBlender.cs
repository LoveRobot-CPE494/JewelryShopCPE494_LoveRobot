using System;
using System.Collections.Generic;

namespace JewelryShop.Core
{
    /// <summary>Weight of each driven emotion preset (ADR-0019); every value is 0..1.</summary>
    public struct ExpressionWeights
    {
        public float Happy;
        public float Relaxed;
        public float Angry;
        public float Sad;

        public static ExpressionWeights Lerp(ExpressionWeights a, ExpressionWeights b, float t) =>
            new ExpressionWeights
            {
                Happy = a.Happy + (b.Happy - a.Happy) * t,
                Relaxed = a.Relaxed + (b.Relaxed - a.Relaxed) * t,
                Angry = a.Angry + (b.Angry - a.Angry) * t,
                Sad = a.Sad + (b.Sad - a.Sad) * t,
            };
    }

    /// <summary>One gesture's Expression, keyed by the Animator state it belongs to.</summary>
    [Serializable]
    public struct ExpressionEntry
    {
        public string StateName;
        public float Happy;
        public float Relaxed;
        public float Angry;
        public float Sad;
    }

    /// <summary>Gesture-to-Expression table and timing (ADR-0019); weights are guesses.</summary>
    [Serializable]
    public struct ExpressionSettings
    {
        public ExpressionEntry[] Entries;
        public float HoldSeconds;
        public float FadeSeconds;
        public float DefaultSmileWeight;

        public static ExpressionSettings Defaults => new ExpressionSettings
        {
            Entries = new[]
            {
                new ExpressionEntry { StateName = "Base Layer.Wave", Happy = 0.8f },
                new ExpressionEntry { StateName = "Base Layer.Bow", Relaxed = 0.8f },
                new ExpressionEntry { StateName = "Base Layer.Point", Happy = 0.5f },
                new ExpressionEntry { StateName = "Base Layer.Nod", Happy = 0.8f },
                new ExpressionEntry { StateName = "Base Layer.Shake", Sad = 0.3f },
                new ExpressionEntry
                {
                    StateName = "Base Layer.Embarrassed", Happy = 0.6f, Relaxed = 0.6f,
                },
            },
            HoldSeconds = 0.5f,
            FadeSeconds = 0.3f,
            DefaultSmileWeight = 0.3f,
        };

        /// <summary>A clean copy: bad or duplicate entries dropped, empty table reset.</summary>
        public ExpressionSettings Validated(List<string> warnings)
        {
            var d = Defaults;
            var kept = new List<ExpressionEntry>();
            foreach (var entry in Entries ?? Array.Empty<ExpressionEntry>())
            {
                if (string.IsNullOrWhiteSpace(entry.StateName))
                {
                    warnings.Add("Expression entry has an empty state name; entry dropped.");
                    continue;
                }

                if (kept.Exists(k => k.StateName == entry.StateName))
                {
                    warnings.Add($"Expression for {entry.StateName} is listed twice; " +
                        "later entry dropped.");
                    continue;
                }

                var e = entry;
                var n = entry.StateName;
                e.Happy = ConfigRule.Clamp(n + " Happy", e.Happy, 0f, 1f, 0f, warnings);
                e.Relaxed = ConfigRule.Clamp(n + " Relaxed", e.Relaxed, 0f, 1f, 0f, warnings);
                e.Angry = ConfigRule.Clamp(n + " Angry", e.Angry, 0f, 1f, 0f, warnings);
                e.Sad = ConfigRule.Clamp(n + " Sad", e.Sad, 0f, 1f, 0f, warnings);
                kept.Add(e);
            }

            if (kept.Count == 0)
            {
                warnings.Add("Expression table has no valid entries; using the default table.");
                kept.AddRange(d.Entries);
            }

            return new ExpressionSettings
            {
                Entries = kept.ToArray(),
                HoldSeconds = ConfigRule.Clamp(nameof(HoldSeconds),
                    HoldSeconds, 0f, 10f, d.HoldSeconds, warnings),
                FadeSeconds = ConfigRule.Clamp(nameof(FadeSeconds),
                    FadeSeconds, 0f, 10f, d.FadeSeconds, warnings),
                DefaultSmileWeight = ConfigRule.Clamp(nameof(DefaultSmileWeight),
                    DefaultSmileWeight, 0f, 1f, d.DefaultSmileWeight, warnings),
            };
        }
    }

    /// <summary>
    /// Turns "which gesture is playing" into emotion weights: hold after the gesture ends, fade to
    /// the default smile, crossfade when a new gesture starts (ADR-0019).
    /// </summary>
    public sealed class ExpressionBlender
    {
        private readonly ExpressionSettings _settings;
        private readonly ExpressionWeights _smile;
        private ExpressionWeights _from;
        private ExpressionWeights _target;
        private bool _hasGesture;
        private float _holdLeft;
        private float _fadeElapsed;

        public ExpressionBlender(ExpressionSettings settings)
        {
            _settings = settings;
            _smile = new ExpressionWeights { Happy = settings.DefaultSmileWeight };
            _from = _target = _smile;
        }

        /// <summary>The weights now.</summary>
        public ExpressionWeights Current
        {
            get
            {
                var t = _settings.FadeSeconds <= 0f ? 1f :
                    Math.Min(1f, _fadeElapsed / _settings.FadeSeconds);
                return ExpressionWeights.Lerp(_from, _target, t);
            }
        }

        /// <summary>A gesture began or repeated: fade to its Expression, restart hold.</summary>
        public void StartGesture(string stateName)
        {
            _from = Current;
            _target = Lookup(stateName);
            _fadeElapsed = 0f;
            _hasGesture = true;
            _holdLeft = _settings.HoldSeconds;
        }

        /// <summary>Advance time; <paramref name="isGestureActive"/> holds it.</summary>
        public ExpressionWeights Tick(float elapsedSeconds, bool isGestureActive)
        {
            _fadeElapsed += elapsedSeconds;
            if (_hasGesture)
            {
                if (isGestureActive)
                    _holdLeft = _settings.HoldSeconds;
                else
                    _holdLeft -= elapsedSeconds;

                if (_holdLeft <= 0f)
                {
                    // Time past the hold already counts toward the fade.
                    var overrun = -_holdLeft;
                    _from = Current;
                    _target = _smile;
                    _fadeElapsed = overrun;
                    _hasGesture = false;
                }
            }

            return Current;
        }

        private ExpressionWeights Lookup(string stateName)
        {
            foreach (var e in _settings.Entries ?? Array.Empty<ExpressionEntry>())
            {
                if (e.StateName == stateName)
                {
                    return new ExpressionWeights
                    {
                        Happy = e.Happy, Relaxed = e.Relaxed, Angry = e.Angry, Sad = e.Sad,
                    };
                }
            }

            return _smile; // an unmapped gesture shows only the default smile
        }
    }
}
