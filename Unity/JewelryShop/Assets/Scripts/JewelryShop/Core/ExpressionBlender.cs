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
        /// <summary>Blink channel over the emotions (ADR-0019); Lerp leaves it 0.</summary>
        public float Blink;

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
        // Blink (ADR-0019). A 0 value means "unset" and takes its code default (see BlinkValue).
        public float BlinkMinSeconds;
        public float BlinkMaxSeconds;
        public float BlinkDurationSeconds;
        public float BlinkHappyThreshold;
        public float BlinkSquintScale;

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
            BlinkMinSeconds = 2f,
            BlinkMaxSeconds = 6f,
            BlinkDurationSeconds = 0.15f,
            BlinkHappyThreshold = 0.5f,
            BlinkSquintScale = 0.6f,
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

            var min = BlinkValue(nameof(BlinkMinSeconds), BlinkMinSeconds, 0.01f, 60f,
                d.BlinkMinSeconds, warnings);
            var max = BlinkValue(nameof(BlinkMaxSeconds), BlinkMaxSeconds, 0.01f, 60f,
                d.BlinkMaxSeconds, warnings);
            if (min > max)
            {
                warnings.Add($"BlinkMinSeconds {min} is above BlinkMaxSeconds {max}; swapped.");
                (min, max) = (max, min);
            }

            return new ExpressionSettings
            {
                BlinkMinSeconds = min,
                BlinkMaxSeconds = max,
                BlinkDurationSeconds = BlinkValue(nameof(BlinkDurationSeconds),
                    BlinkDurationSeconds, 0.01f, 2f, d.BlinkDurationSeconds, warnings),
                BlinkHappyThreshold = BlinkValue(nameof(BlinkHappyThreshold),
                    BlinkHappyThreshold, 0f, 1f, d.BlinkHappyThreshold, warnings),
                BlinkSquintScale = BlinkValue(nameof(BlinkSquintScale),
                    BlinkSquintScale, 0f, 1f, d.BlinkSquintScale, warnings),
                Entries = kept.ToArray(),
                HoldSeconds = ConfigRule.Clamp(nameof(HoldSeconds),
                    HoldSeconds, 0f, 10f, d.HoldSeconds, warnings),
                FadeSeconds = ConfigRule.Clamp(nameof(FadeSeconds),
                    FadeSeconds, 0f, 10f, d.FadeSeconds, warnings),
                DefaultSmileWeight = ConfigRule.Clamp(nameof(DefaultSmileWeight),
                    DefaultSmileWeight, 0f, 1f, d.DefaultSmileWeight, warnings),
            };
        }

        // shortcut: 0 is the "unset" marker (an older asset deserializes new fields as 0), so a
        // threshold or squint scale of exactly 0 cannot be set; use 0.01. Upgrade to a presence
        // flag if that is ever needed.
        private static float BlinkValue(string name, float value, float min, float max,
            float fallback, List<string> warnings) =>
            value == 0f ? fallback : ConfigRule.Clamp(name, value, min, max, fallback, warnings);
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

        // Blink closes over this share of its duration, then opens more slowly.
        private const float CloseShare = 0.35f;
        private readonly Func<float> _random;
        private float _blinkWait;
        private float _blinkElapsed;
        private bool _isBlinking;

        /// <param name="random">Returns 0..1; injected so tests are deterministic.</param>
        public ExpressionBlender(ExpressionSettings settings, Func<float> random = null)
        {
            var rng = new Random();
            _random = random ?? (() => (float)rng.NextDouble());
            _settings = settings;
            _blinkWait = RollInterval();
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

            var w = Current;
            w.Blink = TickBlink(elapsedSeconds, w.Happy);
            return w;
        }

        private float RollInterval() => _settings.BlinkMinSeconds +
            _random() * (_settings.BlinkMaxSeconds - _settings.BlinkMinSeconds);

        // Independent of gestures: a started blink always finishes.
        private float TickBlink(float dt, float happy)
        {
            if (!_isBlinking)
            {
                _blinkWait -= dt;
                if (_blinkWait > 0f)
                    return 0f;
                _isBlinking = true;
                _blinkElapsed = -_blinkWait; // overrun counts into the blink
            }
            else
            {
                _blinkElapsed += dt;
            }

            var duration = _settings.BlinkDurationSeconds;
            if (_blinkElapsed >= duration)
            {
                _isBlinking = false;
                _blinkWait = RollInterval();
                return 0f;
            }

            var p = _blinkElapsed / duration;
            var shape = p < CloseShare ? p / CloseShare :
                1f - (p - CloseShare) / (1f - CloseShare);
            return happy > _settings.BlinkHappyThreshold ?
                shape * _settings.BlinkSquintScale : shape;
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
