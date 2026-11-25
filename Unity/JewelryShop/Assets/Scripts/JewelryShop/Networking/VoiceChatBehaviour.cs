using JewelryShop.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UniVRM10;

namespace JewelryShop.Networking
{
    /// <summary>
    /// Two-way live voice for Staff Call: Microphone -> Opus -> link -> streamed AudioSource, only
    /// while the call is live (In call). Put one next to the StaffCallBehaviour on each end.
    /// Quest: the Staff's voice plays 3D from Cheryll's head, and her mouth follows its loudness.
    /// Staff Console: the Customer's voice plays 2D, and her mouth follows the Staff's microphone.
    /// Blink is not here: StaffExpressionDriver owns it (ADR-0019); this only writes the aa mouth.
    /// </summary>
    public sealed class VoiceChatBehaviour : MonoBehaviour
    {
        private const float FindAvatarIntervalSeconds = 0.5f;
        // A stall this long may have wrapped the one-second microphone clip, so resync.
        private const float MicrophoneStallSeconds = 0.5f;
        private const float SpeakerMinDistanceMeters = 0.5f;
        private const float SpeakerMaxDistanceMeters = 10f;

        [SerializeField] private StaffLinkBehaviour _link;
        [SerializeField] private StaffCallBehaviour _call;
        [Tooltip("Optional; defaults are used with a warning when empty.")]
        [SerializeField] private VoiceConfig _config;
        [Tooltip("Staff Console only: Cheryll's face. The Quest finds her networked copy itself.")]
        [SerializeField] private Vrm10Instance _staffAvatar;
        [Tooltip("Toggles the microphone mute (talk / not talk). Ignored without a keyboard.")]
        [SerializeField] private Key _muteKey = Key.M;

        private VoiceSettings _settings;
        private StaffCallRole _role;
        private OpusVoiceCodec _encoder;
        private OpusVoiceCodec _decoder;
        private PcmRingBuffer _ring;
        private HalfDuplexGate _gate;
        private GameObject _speakerObject;
        private AudioSource _speaker;
        private AudioClip _speakerClip;
        private AudioClip _microphone;
        private IVoiceChannel _subscribedChannel;
        private int _readPosition;
        private float _lastPumpSeconds;
        private bool _isLive;
        private readonly MicrophoneMute _mute = new MicrophoneMute();
        private volatile float _playbackLoudness; // written on the audio thread
        private float _microphoneLoudness;
        private float _mouth;
        private float _nextFindSeconds;
        private CheryllNetworkAvatar _copy;
        private Vrm10Instance _copyAvatar;
        private readonly float[] _frame = new float[OpusVoiceCodec.FrameSamples];
        private readonly float[] _decoded = new float[OpusVoiceCodec.FrameSamples];
        private readonly byte[] _packet = new byte[OpusVoiceCodec.MaxPacketBytes];

        private void Start()
        {
            if (_link == null || _call == null)
            {
                Debug.LogError("VoiceChatBehaviour: link or call not wired; voice is off.", this);
                enabled = false;
                return;
            }
            if (_config == null)
                Debug.LogWarning("VoiceChatBehaviour: no Voice Config; using defaults.", this);
            _settings = _config != null ? _config.Settings : VoiceSettings.Defaults;

            _role = _link.IsHost ? StaffCallRole.Staff : StaffCallRole.Customer;
            if (_role == StaffCallRole.Staff && _staffAvatar == null)
                Debug.LogWarning("VoiceChatBehaviour: Staff avatar not wired; "
                    + "her mouth will not move.", this);
            _encoder = new OpusVoiceCodec();
            _decoder = new OpusVoiceCodec();
            const int sampleRate = OpusVoiceCodec.SampleRate;
            _ring = new PcmRingBuffer(sampleRate,
                Mathf.RoundToInt(sampleRate * _settings.JitterBufferSeconds));
            if (_role == StaffCallRole.Customer && _settings.IsHalfDuplexEnabled)
                _gate = new HalfDuplexGate(_settings.HalfDuplexMuteLoudness,
                    _settings.HalfDuplexReleaseSeconds);

            // The speaker lives on its own child: on the Quest it moves to Cheryll's head, and
            // moving this object would drag the call object and its siblings along.
            _speakerObject = new GameObject("StaffCallVoiceSpeaker");
            _speakerObject.transform.SetParent(transform, false);
            // Streamed clip: Unity pulls samples from the ring on the audio thread.
            _speakerClip = AudioClip.Create(
                "StaffCallVoice", sampleRate, 1, sampleRate, true, OnRead);
            _speaker = _speakerObject.AddComponent<AudioSource>();
            _speaker.clip = _speakerClip;
            _speaker.loop = true;
            // Quest hears Cheryll from her head (3D); the Staff Console hears the Customer plainly.
            var isCustomer = _role == StaffCallRole.Customer;
            _speaker.spatialBlend = isCustomer ? 1f : 0f;
            _speaker.rolloffMode = AudioRolloffMode.Logarithmic;
            _speaker.minDistance = SpeakerMinDistanceMeters;
            _speaker.maxDistance = SpeakerMaxDistanceMeters;
            _speaker.mute = isCustomer; // silent until Cheryll's head is found
            _speaker.Play();
        }

        private void OnDisable()
        {
            if (_ring == null) return; // Start never finished
            Unsubscribe();
            if (_isLive) SetLive(false);
            _mouth = 0f;
            WriteMouth(_role == StaffCallRole.Staff ? _staffAvatar : _copyAvatar);
        }

        private void OnDestroy()
        {
            if (_speakerClip == null) return;
            _speaker.Stop();
            Destroy(_speakerClip);
        }

        /// <summary>True while the user has muted the microphone (talk / not talk).</summary>
        public bool IsMicrophoneMuted => _mute.IsMuted;

        private void OnRead(float[] data)
        {
            _ring.Read(data);
            _playbackLoudness = VoiceMath.RootMeanSquare(data, data.Length);
        }

        private void Update()
        {
            // The link builds its channel in its own Awake and may be rebuilt: follow it.
            Subscribe(_link.Voice);
            var session = _call.Session;
            var isLive = session != null && VoiceMath.IsLive(_role, session.State);
            if (isLive != _isLive) SetLive(isLive);
            if (_isLive) PollMuteKey();
            if (_isLive) PumpMicrophone();
            UpdateMouth();
        }

        private void LateUpdate()
        {
            if (_role != StaffCallRole.Customer) return;
            // Cheryll's copy walks around, so her voice follows her head; no head, no sound.
            var head = _copy != null ? _copy.HeadBone : null;
            _speaker.mute = head == null;
            if (head != null) _speakerObject.transform.position = head.position;
        }

        private void Subscribe(IVoiceChannel channel)
        {
            if (channel == _subscribedChannel) return;
            Unsubscribe();
            _subscribedChannel = channel;
            _subscribedChannel.PacketReceived += OnPacket;
        }

        private void Unsubscribe()
        {
            if (_subscribedChannel == null) return;
            _subscribedChannel.PacketReceived -= OnPacket;
            _subscribedChannel = null;
        }

        private void PollMuteKey()
        {
            var keyboard = Keyboard.current; // null on the Quest: the key is ignored
            if (keyboard == null || !keyboard[_muteKey].wasPressedThisFrame) return;
            Debug.Log(_mute.Toggle() ? "Staff Call voice: microphone muted"
                : "Staff Call voice: microphone live", this);
        }

        private void SetLive(bool isLive)
        {
            _isLive = isLive;
            if (!isLive) _mute.Reset();
            _ring.Clear();
            if (isLive) StartMicrophone();
            else StopMicrophone();
        }

        private void StartMicrophone()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(
                    UnityEngine.Android.Permission.Microphone);
                Debug.LogWarning("VoiceChatBehaviour: microphone permission requested; "
                    + "voice resumes on the next call.", this);
                return;
            }
            if (_settings.IsCommunicationAudioModeRequested)
                SetAndroidAudioMode(3); // MODE_IN_COMMUNICATION
#endif
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("VoiceChatBehaviour: no microphone found; "
                    + "you will not be heard.", this);
                return;
            }
            _microphone = Microphone.Start(null, true, 1, OpusVoiceCodec.SampleRate);
            // shortcut: no resampler, so a device that will not do 16 kHz is refused;
            // add one if a real device needs it.
            if (_microphone != null && _microphone.frequency != OpusVoiceCodec.SampleRate)
            {
                Debug.LogError($"VoiceChatBehaviour: microphone runs at {_microphone.frequency} "
                    + "Hz, not 16000; you will not be heard.", this);
                StopMicrophone();
                return;
            }
            _readPosition = 0;
            _lastPumpSeconds = Time.unscaledTime;
        }

        private void StopMicrophone()
        {
            _microphoneLoudness = 0f;
            if (_microphone == null) return;
            Microphone.End(null);
            Destroy(_microphone);
            _microphone = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_settings.IsCommunicationAudioModeRequested)
                SetAndroidAudioMode(0); // MODE_NORMAL
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void SetAndroidAudioMode(int mode)
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var audio = activity.Call<AndroidJavaObject>("getSystemService", "audio");
                audio.Call("setMode", mode);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"VoiceChatBehaviour: cannot set the Android audio mode: {e.Message}", this);
            }
        }
#endif

        private void PumpMicrophone()
        {
            if (_microphone == null) return;
            var position = Microphone.GetPosition(null);
            var length = _microphone.samples;
            var now = Time.unscaledTime;
            if (now - _lastPumpSeconds >= MicrophoneStallSeconds)
                _readPosition = position; // a long stall may have wrapped the clip: skip ahead
            _lastPumpSeconds = now;
            var available = (position - _readPosition + length) % length;
            while (available >= OpusVoiceCodec.FrameSamples)
            {
                _microphone.GetData(_frame, _readPosition);
                _readPosition = (_readPosition + OpusVoiceCodec.FrameSamples) % length;
                available -= OpusVoiceCodec.FrameSamples;
                SendFrame();
            }
        }

        private void SendFrame()
        {
            _microphoneLoudness = _mute.LocalLoudness(
                VoiceMath.RootMeanSquare(_frame, _frame.Length));
            // Half-duplex: do not send Cheryll's own voice back to her. The gate keeps
            // listening even while the user is muted.
            var isHalfDuplexMuted = _gate != null && _gate.UpdateIsMuted(
                _playbackLoudness, Time.realtimeSinceStartupAsDouble);
            if (!_mute.CanSend(isHalfDuplexMuted)) return;
            var length = _encoder.Encode(_frame, _packet);
            _link.Voice.Send(_packet, length);
        }

        private void OnPacket(byte[] packet, int length)
        {
            if (!_isLive) return; // voice is live only while In call
            try
            {
                var count = _decoder.Decode(packet, length, _decoded);
                _ring.Write(_decoded, count);
            }
            catch (System.Exception e)
            {
                // A corrupt packet is dropped; the next one recovers.
                Debug.LogWarning(
                    $"VoiceChatBehaviour: dropped a bad voice packet: {e.Message}", this);
            }
        }

        private void UpdateMouth()
        {
            // The Staff's mouth follows her microphone; the Customer sees it follow what she hears.
            var loudness = !_isLive ? 0f
                : _role == StaffCallRole.Staff ? _microphoneLoudness : _playbackLoudness;
            var target = VoiceMath.MouthOpen(
                loudness, _settings.MouthSilenceLoudness, _settings.MouthFullLoudness);
            _mouth = Mathf.MoveTowards(
                _mouth, target, _settings.MouthSpeedPerSecond * Time.deltaTime);

            var avatar = _staffAvatar;
            if (_role == StaffCallRole.Customer)
            {
                if (_copy == null)
                {
                    _copyAvatar = null; // she is gone (or not here yet): close her mouth
                    _mouth = 0f;
                    if (Time.unscaledTime < _nextFindSeconds) return;
                    _nextFindSeconds = Time.unscaledTime + FindAvatarIntervalSeconds;
                    _copy = FindAnyObjectByType<CheryllNetworkAvatar>();
                    if (_copy != null) _copyAvatar = _copy.GetComponent<Vrm10Instance>();
                }
                avatar = _copyAvatar;
            }
            WriteMouth(avatar);
        }

        // Only aa: StaffExpressionDriver owns happy, relaxed, angry, sad and blink.
        private void WriteMouth(Vrm10Instance avatar)
        {
            if (avatar != null && avatar.Runtime != null)
                avatar.Runtime.Expression.SetWeight(ExpressionKey.Aa, _mouth);
        }
    }
}
