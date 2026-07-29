using System;
using System.Threading;
using CalmSpace.Audio;
using CalmSpace.Haptics;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace CalmSpace.Fasteners
{
    /// <summary>
    /// One screw the player turns out by holding a finger on it. The screw
    /// rises and spins while the hold lasts, ticks once per whole turn, and
    /// stops taking raycasts the moment it is out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScrewController : MonoBehaviour
    {
        [SerializeField]
        private FastenerPanel _panel;

        [SerializeField]
        [Min(1)]
        private int _turnsRequired = 3;

        [SerializeField]
        [Min(0.05f)]
        private float _secondsPerTurn = 0.42f;

        [SerializeField]
        [Min(0f)]
        private float _riseHeight = 0.32f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _tickIntensity = 0.55f;

        [SerializeField]
        [Min(0f)]
        private float _settleDurationSeconds = 0.22f;

        private IHapticService _hapticService;
        private IAsmrAudioService _audioService;
        private ScrewRotationModel _model;
        private Collider[] _colliders = Array.Empty<Collider>();
        private Vector3 _seatedLocalPosition;
        private Quaternion _seatedLocalRotation;
        private Vector3 _seatedLocalScale;
        private bool _initialized;
        private bool _isRemoved;
        private bool _isHeld;

        public event Action<ScrewController> Removed;

        public FastenerPanel Panel => _panel;

        public bool IsRemoved => _isRemoved;

        public bool IsHeld => _isHeld;

        public float Progress
        {
            get
            {
                EnsureInitialized();
                return _model.Progress;
            }
        }

        [Inject]
        public void Construct(
            IHapticService hapticService,
            IAsmrAudioService audioService)
        {
            _hapticService = hapticService ??
                throw new ArgumentNullException(
                    nameof(hapticService));
            _audioService = audioService ??
                throw new ArgumentNullException(
                    nameof(audioService));
        }

        public bool BeginHold()
        {
            EnsureInitialized();
            if (_isRemoved || !isActiveAndEnabled)
            {
                return false;
            }

            _isHeld = true;
            return true;
        }

        public void ContinueHold(float deltaSeconds)
        {
            if (!_isHeld || _isRemoved)
            {
                return;
            }

            ScrewAdvanceResult result = _model.Advance(deltaSeconds);
            ApplyPose();

            for (var turn = 0;
                 turn < result.NewlyCompletedTurns;
                 turn++)
            {
                _hapticService?.PlayDragTick(_tickIntensity);
            }

            if (result.IsComplete)
            {
                CompleteRemoval();
                return;
            }
        }

        public void EndHold()
        {
            _isHeld = false;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Transform ownTransform = transform;
            _seatedLocalPosition = ownTransform.localPosition;
            _seatedLocalRotation = ownTransform.localRotation;
            _seatedLocalScale = ownTransform.localScale;
            _colliders = GetComponentsInChildren<Collider>(true);
            _model = new ScrewRotationModel(
                Mathf.Max(1, _turnsRequired),
                Mathf.Max(0.05f, _secondsPerTurn));
        }

        private void ApplyPose()
        {
            Transform ownTransform = transform;
            ownTransform.localPosition = _seatedLocalPosition +
                Vector3.up * (_riseHeight * _model.Progress);
            ownTransform.localRotation = _seatedLocalRotation *
                Quaternion.Euler(0f, -_model.RotationDegrees, 0f);
        }

        private void CompleteRemoval()
        {
            _isRemoved = true;
            _isHeld = false;
            SetCollidersEnabled(false);
            InvokeRemoved();
            PlayRemovalFeedback();
            PlaySettleAsync(this.GetCancellationTokenOnDestroy())
                .Forget();
        }

        private void PlayRemovalFeedback()
        {
            try
            {
                _hapticService?.PlaySnap();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            try
            {
                _audioService?.PlaySnap(transform.position);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void SetCollidersEnabled(bool value)
        {
            for (var index = 0; index < _colliders.Length; index++)
            {
                Collider collider = _colliders[index];
                if (collider != null)
                {
                    collider.enabled = value;
                }
            }
        }

        private async UniTaskVoid PlaySettleAsync(
            CancellationToken cancellationToken)
        {
            if (_settleDurationSeconds > 0f)
            {
                var elapsed = 0f;
                try
                {
                    while (elapsed < _settleDurationSeconds)
                    {
                        await UniTask.Yield(
                            PlayerLoopTiming.Update,
                            cancellationToken);
                        elapsed += Time.deltaTime;
                        float remaining = 1f - Mathf.Clamp01(
                            elapsed / _settleDurationSeconds);
                        transform.localScale =
                            _seatedLocalScale * remaining;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (this == null)
            {
                return;
            }

            transform.localScale = _seatedLocalScale;
            gameObject.SetActive(false);
        }

        private void InvokeRemoved()
        {
            var handler = Removed;
            if (handler == null)
            {
                return;
            }

            foreach (Action<ScrewController> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
