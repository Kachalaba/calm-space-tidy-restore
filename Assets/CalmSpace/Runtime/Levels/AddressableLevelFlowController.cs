using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using CalmSpace.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;
using VContainer.Unity;

namespace CalmSpace.Levels
{
    public enum LevelAssetTransitionState
    {
        Idle = 0,
        ReleasingInstance = 1,
        UnloadingUnusedAssets = 2,
        LoadingAsset = 3,
        InstantiatingLevel = 4
    }

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
        private readonly LevelCatalog _catalog;
        private readonly IObjectResolver _resolver;
        private readonly IUndoHistory _undoHistory;
        private readonly Transform _levelRoot;
        private readonly SemaphoreSlim _transitionGate =
            new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private AsyncOperationHandle<GameObject> _levelAssetHandle;
        private GameObject _levelInstance;
        private bool _hasLevelAssetHandle;
        private bool _disposed;

        public AddressableLevelFlowController(
            LevelCatalog catalog,
            IObjectResolver resolver,
            IUndoHistory undoHistory,
            Transform levelRoot)
        {
            _catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
            _resolver = resolver ??
                throw new ArgumentNullException(nameof(resolver));
            _undoHistory = undoHistory ??
                throw new ArgumentNullException(nameof(undoHistory));
            _levelRoot = levelRoot ??
                throw new ArgumentNullException(nameof(levelRoot));
        }

        public LevelBase CurrentLevel { get; private set; }

        public int CurrentLevelIndex { get; private set; } = -1;

        public bool IsLoading { get; private set; }

        public LevelAssetTransitionState TransitionState
        {
            get;
            private set;
        } = LevelAssetTransitionState.Idle;

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
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_catalog.Count <= 0)
            {
                return false;
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
                    await ReleaseAndUnloadUnusedAssetsAsync();
                    token.ThrowIfCancellationRequested();
                }
                finally
                {
                    TransitionState = LevelAssetTransitionState.Idle;
                    IsLoading = false;
                    _transitionGate.Release();
                }
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
            ReleaseCurrentLevelImmediate();
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

                    await ReleaseAndUnloadUnusedAssetsAsync();
                    token.ThrowIfCancellationRequested();

                    TransitionState =
                        LevelAssetTransitionState.LoadingAsset;
                    var handle =
                        Addressables.LoadAssetAsync<GameObject>(
                            entry.Prefab.RuntimeKey);
                    var ownsHandle = true;
                    GameObject instance = null;

                    try
                    {
                        var prefab = await handle.ToUniTask(
                            cancellationToken: token);
                        token.ThrowIfCancellationRequested();

                        if (handle.Status !=
                                AsyncOperationStatus.Succeeded ||
                            prefab == null)
                        {
                            return false;
                        }

                        TransitionState =
                            LevelAssetTransitionState.InstantiatingLevel;
                        instance = _resolver.Instantiate(
                            prefab,
                            _levelRoot,
                            false);
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

                        _levelAssetHandle = handle;
                        _hasLevelAssetHandle = true;
                        _levelInstance = instance;
                        ownsHandle = false;
                        instance = null;
                        CurrentLevel = level;
                        CurrentLevelIndex = index;
                        return true;
                    }
                    finally
                    {
                        if (instance != null)
                        {
                            UnityEngine.Object.Destroy(instance);
                        }

                        if (ownsHandle && handle.IsValid())
                        {
                            Addressables.Release(handle);
                        }
                    }
                }
                finally
                {
                    TransitionState = LevelAssetTransitionState.Idle;
                    IsLoading = false;
                    _transitionGate.Release();
                }
            }
        }

        private async UniTask ReleaseAndUnloadUnusedAssetsAsync()
        {
            bool released = ReleaseCurrentLevelImmediate();
            if (!released)
            {
                return;
            }

            // Destroy is deferred. Wait until Unity has retired the instance
            // before sweeping textures, meshes, and clips that lost their last
            // Addressables reference.
            await UniTask.Yield(
                PlayerLoopTiming.LastPostLateUpdate,
                _lifetimeCancellation.Token);

            TransitionState =
                LevelAssetTransitionState.UnloadingUnusedAssets;
            AsyncOperation unloadOperation =
                Resources.UnloadUnusedAssets();
            await unloadOperation.ToUniTask(
                cancellationToken: _lifetimeCancellation.Token);
        }

        private bool ReleaseCurrentLevelImmediate()
        {
            CurrentLevel = null;
            CurrentLevelIndex = -1;

            bool released = false;
            if (_levelInstance != null)
            {
                TransitionState =
                    LevelAssetTransitionState.ReleasingInstance;
                _levelInstance.SetActive(false);
                UnityEngine.Object.Destroy(_levelInstance);
                _levelInstance = null;
                released = true;
            }

            if (_hasLevelAssetHandle)
            {
                TransitionState =
                    LevelAssetTransitionState.ReleasingInstance;
                var handle = _levelAssetHandle;
                _levelAssetHandle = default;
                _hasLevelAssetHandle = false;
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                released = true;
            }

            // Deactivating a held screw may finalize its current hold and
            // enqueue one last undo command. Clear after deactivation so no
            // command from the retired level can leak into the next level.
            _undoHistory.Clear();
            return released;
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
