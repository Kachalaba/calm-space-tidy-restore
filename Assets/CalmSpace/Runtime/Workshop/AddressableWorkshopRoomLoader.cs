using System;
using System.Threading;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CalmSpace.Workshop
{
    public interface IWorkshopRoomLoader : IDisposable
    {
        bool IsLoaded { get; }
        WorkshopRoomPresenter Presenter { get; }

        UniTask<WorkshopRoomPresenter> LoadAsync(
            string chapterId,
            Transform parent,
            CancellationToken cancellationToken);

        UniTask UnloadAsync(CancellationToken cancellationToken);
    }

    /// <summary>
    /// The single owner of the workshop room Addressable handle and instance.
    /// Nothing else in the project loads, retains, or releases that asset: the
    /// handle is created at most once per chapter, concurrent callers share
    /// one in-flight load, a load that lands after cancellation is released
    /// immediately, and unload or dispose releases exactly once.
    /// </summary>
    public sealed class AddressableWorkshopRoomLoader : IWorkshopRoomLoader
    {
        public const string AddressFormat = "workshop/{0}/room";

        private AsyncOperationHandle<GameObject> _handle;
        private bool _hasHandle;
        private WorkshopRoomPresenter _presenter;
        private string _chapterId = string.Empty;
        private Transform _parent;
        private UniTask<WorkshopRoomPresenter> _inFlight;
        private CancellationTokenSource _loadCancellation;
        private int _loadGeneration;
        private bool _loading;
        private bool _disposed;

        public bool IsLoaded => _presenter != null;

        public WorkshopRoomPresenter Presenter => _presenter;

        public static string BuildAddress(string chapterId)
        {
            return string.Format(
                AddressFormat,
                string.IsNullOrWhiteSpace(chapterId)
                    ? string.Empty
                    : chapterId.Trim());
        }

        public UniTask<WorkshopRoomPresenter> LoadAsync(
            string chapterId,
            Transform parent,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(AddressableWorkshopRoomLoader));
            }

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                throw new ArgumentException(
                    "A chapter id is required.", nameof(chapterId));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            cancellationToken.ThrowIfCancellationRequested();

            string normalizedChapterId = chapterId.Trim();
            if (IsSameTarget(normalizedChapterId, parent))
            {
                if (_presenter != null)
                {
                    return UniTask.FromResult(_presenter);
                }

                if (_loading)
                {
                    return _inFlight;
                }
            }
            else if (_loading)
            {
                throw new InvalidOperationException(
                    "The workshop room loader is already loading a different " +
                    "chapter or parent.");
            }
            else
            {
                // Retarget: the previous room must be released before another
                // handle is taken, or the first one leaks.
                ReleaseCurrent();
            }

            _loading = true;
            _chapterId = normalizedChapterId;
            _parent = parent;
            _loadCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            _inFlight = LoadCoreAsync(
                normalizedChapterId,
                parent,
                _loadCancellation.Token).Preserve();
            return _inFlight;
        }

        private async UniTask<WorkshopRoomPresenter> LoadCoreAsync(
            string chapterId,
            Transform parent,
            CancellationToken cancellationToken)
        {
            // A load that has been superseded by unload, dispose, or a
            // retarget must never write back into loader state.
            int generation = ++_loadGeneration;
            AsyncOperationHandle<GameObject> handle =
                Addressables.InstantiateAsync(
                    BuildAddress(chapterId), parent, false);
            GameObject instance;
            try
            {
                instance = await handle.ToUniTask(
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The Addressables operation keeps running after the caller
                // gives up, so wait for it and release the late instance
                // instead of leaking it.
                await ReleaseLateAsync(handle);
                ClearLoading(generation);
                throw;
            }
            catch
            {
                ReleaseHandle(ref handle);
                ClearLoading(generation);
                throw;
            }

            WorkshopRoomPresenter presenter = instance == null
                ? null
                : instance.GetComponent<WorkshopRoomPresenter>();
            if (presenter == null)
            {
                ReleaseHandle(ref handle);
                ClearLoading(generation);
                throw new InvalidOperationException(
                    "The workshop room prefab has no WorkshopRoomPresenter.");
            }

            if (cancellationToken.IsCancellationRequested ||
                _disposed ||
                generation != _loadGeneration)
            {
                ReleaseHandle(ref handle);
                ClearLoading(generation);
                cancellationToken.ThrowIfCancellationRequested();
                throw new ObjectDisposedException(
                    nameof(AddressableWorkshopRoomLoader));
            }

            _handle = handle;
            _hasHandle = true;
            _presenter = presenter;
            _chapterId = chapterId;
            _parent = parent;
            _loading = false;
            return presenter;
        }

        private void ClearLoading(int generation)
        {
            if (generation == _loadGeneration)
            {
                _loading = false;
            }
        }

        public UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseCurrent();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseCurrent();
        }

        private bool IsSameTarget(string chapterId, Transform parent)
        {
            return string.Equals(
                    _chapterId, chapterId, StringComparison.Ordinal) &&
                ReferenceEquals(_parent, parent);
        }

        private void ReleaseCurrent()
        {
            // Stop any in-flight load first, otherwise it can land afterwards
            // and quietly re-adopt a handle this loader no longer owns.
            _loadGeneration++;
            if (_loadCancellation != null)
            {
                _loadCancellation.Cancel();
                _loadCancellation.Dispose();
                _loadCancellation = null;
            }

            _presenter = null;
            _parent = null;
            _chapterId = string.Empty;
            _loading = false;
            if (!_hasHandle)
            {
                return;
            }

            _hasHandle = false;
            AsyncOperationHandle<GameObject> handle = _handle;
            _handle = default;
            ReleaseHandle(ref handle);
        }

        private static async UniTask ReleaseLateAsync(
            AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            try
            {
                await handle.ToUniTask();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            ReleaseHandle(ref handle);
        }

        private static void ReleaseHandle(
            ref AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            AsyncOperationHandle<GameObject> released = handle;
            handle = default;
            Addressables.ReleaseInstance(released);
        }
    }
}
