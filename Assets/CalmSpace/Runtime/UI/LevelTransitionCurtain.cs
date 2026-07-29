using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CalmSpace.UI
{
    public enum LevelTransitionCurtainState
    {
        Clear = 0,
        FadingToOpaque = 1,
        Opaque = 2,
        FadingToClear = 3
    }

    /// <summary>
    /// A small, cancellation-safe state machine for the loading curtain.
    /// The curtain blocks interaction before expensive asset cleanup begins
    /// and renders at least one fully opaque frame before returning.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LevelTransitionCurtain : MonoBehaviour
    {
        [SerializeField, Min(0.01f)]
        private float _fadeDurationSeconds = 0.18f;

        private CanvasGroup _canvasGroup;
        private CancellationTokenSource _lifetimeCancellation;

        public LevelTransitionCurtainState State { get; private set; } =
            LevelTransitionCurtainState.Clear;

        public bool IsOpaque =>
            State == LevelTransitionCurtainState.Opaque;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _lifetimeCancellation = new CancellationTokenSource();
            SetClearImmediate();
        }

        public async UniTask FadeToOpaqueAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (State == LevelTransitionCurtainState.Opaque)
            {
                await YieldOpaqueFrameAsync(cancellationToken);
                return;
            }

            using (var linkedCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       _lifetimeCancellation.Token))
            {
                var token = linkedCancellation.Token;
                State = LevelTransitionCurtainState.FadingToOpaque;
                _canvasGroup.gameObject.SetActive(true);
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;

                await FadeAsync(
                    _canvasGroup.alpha,
                    1f,
                    token);

                _canvasGroup.alpha = 1f;
                State = LevelTransitionCurtainState.Opaque;
                await YieldOpaqueFrameAsync(token);
            }
        }

        public async UniTask FadeToClearAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (State == LevelTransitionCurtainState.Clear)
            {
                SetClearImmediate();
                return;
            }

            using (var linkedCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       _lifetimeCancellation.Token))
            {
                var token = linkedCancellation.Token;
                State = LevelTransitionCurtainState.FadingToClear;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;

                try
                {
                    await FadeAsync(
                        _canvasGroup.alpha,
                        0f,
                        token);
                }
                finally
                {
                    if (!token.IsCancellationRequested)
                    {
                        SetClearImmediate();
                    }
                }
            }
        }

        public void SetClearImmediate()
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            State = LevelTransitionCurtainState.Clear;
            if (_canvasGroup.gameObject.activeSelf)
            {
                _canvasGroup.gameObject.SetActive(false);
            }
        }

        private async UniTask FadeAsync(
            float from,
            float to,
            CancellationToken cancellationToken)
        {
            var elapsed = 0f;
            while (elapsed < _fadeDurationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var linear = Mathf.Clamp01(
                    elapsed / _fadeDurationSeconds);
                var eased = linear * linear * (3f - 2f * linear);
                _canvasGroup.alpha = Mathf.LerpUnclamped(
                    from,
                    to,
                    eased);
                await UniTask.Yield(
                    PlayerLoopTiming.Update,
                    cancellationToken);
            }
        }

        private static async UniTask YieldOpaqueFrameAsync(
            CancellationToken cancellationToken)
        {
            // Crossing from LastPostLateUpdate to the next Update guarantees
            // that the opaque UI was submitted for one rendered frame before
            // an Addressables release or Resources.UnloadUnusedAssets starts.
            await UniTask.Yield(
                PlayerLoopTiming.LastPostLateUpdate,
                cancellationToken);
            await UniTask.Yield(
                PlayerLoopTiming.Update,
                cancellationToken);
        }

        private void EnsureInitialized()
        {
            EnsureCanvasGroup();
            _lifetimeCancellation ??= new CancellationTokenSource();
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_canvasGroup == null)
            {
                throw new InvalidOperationException(
                    "LevelTransitionCurtain requires a CanvasGroup.");
            }
        }

        private void OnDestroy()
        {
            if (_lifetimeCancellation == null)
            {
                return;
            }

            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _fadeDurationSeconds =
                Mathf.Max(0.01f, _fadeDurationSeconds);
        }
#endif
    }
}
