using System;
using System.Threading;
using CalmSpace.Monetization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace CalmSpace.Levels
{
    public interface ILevelFlowController
    {
        LevelBase CurrentLevel { get; }

        int CurrentLevelIndex { get; }

        bool IsLoading { get; }

        UniTask<bool> LoadFirstLevelAsync(
            CancellationToken cancellationToken = default);

        UniTask<bool> LoadLevelAsync(
            string levelId,
            CancellationToken cancellationToken = default);

        UniTask<bool> LoadNextLevelAsync(
            bool tryInterstitial,
            CancellationToken cancellationToken = default);

        UniTask UnloadCurrentLevelAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Keeps one Addressable level prefab alive inside the single gameplay
    /// scene. All loaded objects are injected through the scene's VContainer.
    /// </summary>
    public sealed class AddressableLevelFlowController :
        ILevelFlowController,
        IDisposable
    {
        private const string BetweenLevelsPlacement =
            "between-levels";

        private readonly LevelCatalog _catalog;
        private readonly IObjectResolver _resolver;
        private readonly IMonetizationManager _monetization;
        private readonly Transform _levelRoot;
        private readonly SemaphoreSlim _transitionGate =
            new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private AsyncOperationHandle<GameObject> _instanceHandle;
        private bool _hasInstanceHandle;
        private bool _disposed;

        public AddressableLevelFlowController(
            LevelCatalog catalog,
            IObjectResolver resolver,
            IMonetizationManager monetization,
            Transform levelRoot)
        {
            _catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
            _resolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));
            _monetization = monetization ??
                throw new ArgumentNullException(nameof(monetization));
            _levelRoot = levelRoot ??
                throw new ArgumentNullException(nameof(levelRoot));
        }

        public LevelBase CurrentLevel { get; private set; }

        public int CurrentLevelIndex { get; private set; } = -1;

        public bool IsLoading { get; private set; }

        public UniTask<bool> LoadFirstLevelAsync(
            CancellationToken cancellationToken = default)
        {
            return LoadIndexAsync(0, cancellationToken);
        }

        public UniTask<bool> LoadLevelAsync(
            string levelId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            return _catalog.TryFindEntry(
                levelId,
                out var index,
                out _)
                ? LoadIndexAsync(index, cancellationToken)
                : UniTask.FromResult(false);
        }

        public async UniTask<bool> LoadNextLevelAsync(
            bool tryInterstitial,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_catalog.Count <= 0)
            {
                return false;
            }

            if (tryInterstitial && CurrentLevel != null)
            {
                // The policy layer atomically rejects this call if gameplay,
                // a drag, or another fullscreen presentation is still active.
                await _monetization
                    .TryShowInterstitialBetweenLevelsAsync(
                        BetweenLevelsPlacement,
                        cancellationToken);
            }

            var nextIndex = CurrentLevelIndex < 0
                ? 0
                : (CurrentLevelIndex + 1) % _catalog.Count;
            return await LoadIndexAsync(nextIndex, cancellationToken);
        }

        public async UniTask UnloadCurrentLevelAsync(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _transitionGate.WaitAsync(cancellationToken);
            try
            {
                ReleaseCurrentLevel();
            }
            finally
            {
                _transitionGate.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            ReleaseCurrentLevel();
            _lifetimeCancellation.Dispose();
        }

        private async UniTask<bool> LoadIndexAsync(
            int index,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (var linkedCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       _lifetimeCancellation.Token))
            {
                var token = linkedCancellation.Token;
                await _transitionGate.WaitAsync(token);
                IsLoading = true;

                try
                {
                    if (!_catalog.TryGetEntry(index, out var entry))
                    {
                        return false;
                    }

                    ReleaseCurrentLevel();
                    token.ThrowIfCancellationRequested();

                    var handle = Addressables.InstantiateAsync(
                        entry.Prefab.RuntimeKey,
                        _levelRoot,
                        false,
                        true);
                    var ownsHandle = true;

                    try
                    {
                        var instance = await handle.ToUniTask(
                            cancellationToken: token);
                        token.ThrowIfCancellationRequested();

                        if (handle.Status !=
                                AsyncOperationStatus.Succeeded ||
                            instance == null)
                        {
                            return false;
                        }

                        _resolver.InjectGameObject(instance);
                        var level =
                            instance.GetComponentInChildren<LevelBase>(
                                true);
                        if (level == null ||
                            !level.InitLevel(entry.Definition))
                        {
                            return false;
                        }

                        token.ThrowIfCancellationRequested();
                        if (_disposed)
                        {
                            return false;
                        }

                        _instanceHandle = handle;
                        _hasInstanceHandle = true;
                        ownsHandle = false;
                        CurrentLevel = level;
                        CurrentLevelIndex = index;
                        return true;
                    }
                    finally
                    {
                        if (ownsHandle && handle.IsValid())
                        {
                            Addressables.ReleaseInstance(handle);
                        }
                    }
                }
                finally
                {
                    IsLoading = false;
                    _transitionGate.Release();
                }
            }
        }

        private void ReleaseCurrentLevel()
        {
            CurrentLevel = null;
            CurrentLevelIndex = -1;

            if (!_hasInstanceHandle)
            {
                return;
            }

            var handle = _instanceHandle;
            _instanceHandle = default;
            _hasInstanceHandle = false;

            if (handle.IsValid())
            {
                Addressables.ReleaseInstance(handle);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(AddressableLevelFlowController));
            }
        }
    }
}
