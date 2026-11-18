using System;

namespace JewelryShop.Core
{
    /// <summary>Timing rules for a Gem Model's return to the Display Case: wait, fly, arrive.
    /// The caller supplies the time deltas.</summary>
    public class GemReturnTimer
    {
        /// <summary>Where the return stands.</summary>
        public enum PhaseKind { Idle, Waiting, Flying }

        private readonly float _delaySeconds;
        private readonly float _flightDurationSeconds;
        private float _elapsedSeconds;

        /// <summary>Delay before the fly home and the length of the fly, in seconds.</summary>
        public GemReturnTimer(float delaySeconds, float flightDurationSeconds)
        {
            _delaySeconds = Math.Max(0f, delaySeconds);
            _flightDurationSeconds = Math.Max(0f, flightDurationSeconds);
        }

        /// <summary>Idle, waiting out the delay, or flying home.</summary>
        public PhaseKind Phase { get; private set; }

        /// <summary>0..1 through the fly home; 0 outside Flying.</summary>
        public float FlightProgress =>
            Phase != PhaseKind.Flying ? 0f
            : _flightDurationSeconds <= 0f ? 1f
            : Math.Min(1f, _elapsedSeconds / _flightDurationSeconds);

        /// <summary>The Gem Model was let go: start (or restart) the delay.
        /// No-op while flying, so an immediate return is not knocked back to waiting.</summary>
        public void Release()
        {
            if (Phase == PhaseKind.Flying) return;
            Phase = PhaseKind.Waiting;
            _elapsedSeconds = 0f;
        }

        /// <summary>The Gem Model was grabbed: cancel any pending or running return.</summary>
        public void Grab() => Phase = PhaseKind.Idle;

        /// <summary>Skip the delay and start flying now (Order Tray).</summary>
        public void ReturnNow()
        {
            Phase = PhaseKind.Flying;
            _elapsedSeconds = 0f;
        }

        /// <summary>Advance by deltaSeconds; true on the call where the flight finishes.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (Phase == PhaseKind.Idle) return false;
            _elapsedSeconds += deltaSeconds;
            if (Phase == PhaseKind.Waiting)
            {
                if (_elapsedSeconds < _delaySeconds) return false;
                _elapsedSeconds -= _delaySeconds;
                Phase = PhaseKind.Flying;
            }
            if (_elapsedSeconds < _flightDurationSeconds) return false;
            Phase = PhaseKind.Idle;
            return true;
        }
    }
}
