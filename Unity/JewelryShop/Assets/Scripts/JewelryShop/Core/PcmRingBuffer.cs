using System;

namespace JewelryShop.Core
{
    /// <summary>
    /// Jitter buffer between the network (main thread, Write) and the audio thread (Read).
    /// Plays silence until <c>primeSamples</c> are queued, and again after it runs dry.
    /// Overflow drops the oldest samples so latency never grows.
    /// </summary>
    public sealed class PcmRingBuffer
    {
        private readonly float[] _data;
        private readonly int _primeSamples;
        private readonly object _lock = new object();
        private int _head;
        private int _count;
        private bool _isPlaying;

        /// <summary>A buffer holding up to <paramref name="capacity"/> samples.</summary>
        public PcmRingBuffer(int capacity, int primeSamples)
        {
            _data = new float[capacity];
            _primeSamples = Math.Min(primeSamples, capacity);
        }

        /// <summary>Queue the first <paramref name="count"/> samples.</summary>
        public void Write(float[] samples, int count)
        {
            lock (_lock)
            {
                for (var i = 0; i < count; i++)
                {
                    if (_count == _data.Length) { _head = (_head + 1) % _data.Length; _count--; }
                    _data[(_head + _count) % _data.Length] = samples[i];
                    _count++;
                }
            }
        }

        /// <summary>Fill <paramref name="destination"/>; whatever is not available is silence.</summary>
        public void Read(float[] destination)
        {
            lock (_lock)
            {
                if (!_isPlaying && _count >= _primeSamples) _isPlaying = true;
                var readCount = _isPlaying ? Math.Min(destination.Length, _count) : 0;
                for (var i = 0; i < readCount; i++)
                {
                    destination[i] = _data[_head];
                    _head = (_head + 1) % _data.Length;
                }
                _count -= readCount;
                for (var i = readCount; i < destination.Length; i++) destination[i] = 0f;
                if (_count == 0) _isPlaying = false;
            }
        }

        /// <summary>Throw away everything queued (call ended).</summary>
        public void Clear()
        {
            lock (_lock) { _head = 0; _count = 0; _isPlaying = false; }
        }
    }
}
