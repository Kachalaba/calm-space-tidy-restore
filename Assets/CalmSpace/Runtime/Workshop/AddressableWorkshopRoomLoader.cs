using System;
using System.Threading;
using System.Threading.Tasks;
using CalmSpace.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CalmSpace.Workshop
{
    internal interface IWorkshopRoomOperationAdapter
    {
        AsyncOperationHandle<GameObject> InstantiateAsync(
            string address,
            Transform parent);

        void ReleaseInstance(AsyncOperationHandle<GameObject> handle);
    }

    internal sealed class AddressablesWorkshopRoomOperationAdapter :
        IWorkshopRoomOperationAdapter
    {
        public AsyncOperationHandle<GameObject> InstantiateAsync(
            string address,
            Transform parent)
        {
            return Addressables.InstantiateAsync(address, parent, false);
        }

        public void ReleaseInstance(AsyncOperationHandle<GameObject> handle)
        {
            Addressables.ReleaseInstance(handle);
        }
    }

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

        private readonly IWorkshopRoomOperationAdapter _operationAdapter;
        private AsyncOperationHandle<GameObject> _handle;
        private bool _hasHandle;
        private WorkshopRoomPresenter _presenter;
        private string _chapterId = string.Empty;
        private Transform _parent;
        private Task<WorkshopRoomPresenter> _inFlight;
        private CancellationTokenSource _loadCancellation;
        private int _loadGeneration;
        private bool _loading;
        private bool _disposed;

        public bool IsLoaded => _presenter != null;

        public WorkshopRoomPresenter Presenter => _presenter;

        public AddressableWorkshopRoomLoader()
            : this(new AddressablesWorkshopRoomOperationAdapter())
        {
        }

        internal AddressableWorkshopRoomLoader(
            IWorkshopRoomOperationAdapter operationAdapter)
        {
            _operationAdapter = operationAdapter ??
                throw new ArgumentNullException(nameof(operationAdapter));
        }

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
                    return AwaitForCaller(_inFlight, cancellationToken);
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
            var cancellationOwner = new CancellationTokenSource();
            CancellationToken operationToken = cancellationOwner.Token;
            _loadCancellation = cancellationOwner;
            int generation = ++_loadGeneration;
            _inFlight = LoadCoreAsync(
                normalizedChapterId,
                parent,
                generation,
                cancellationOwner,
                operationToken).AsTask();
            return AwaitForCaller(_inFlight, cancellationToken);
        }

        private static UniTask<WorkshopRoomPresenter> AwaitForCaller(
            Task<WorkshopRoomPresenter> inFlight,
            CancellationToken cancellationToken)
        {
            UniTask<WorkshopRoomPresenter> waiter = inFlight.AsUniTask();
            return cancellationToken.CanBeCanceled
                ? waiter.AttachExternalCancellation(cancellationToken)
                : waiter;
        }

        private async UniTask<WorkshopRoomPresenter> LoadCoreAsync(
            string chapterId,
            Transform parent,
            int generation,
            CancellationTokenSource cancellationOwner,
            CancellationToken operationToken)
        {
            // A load that has been superseded by unload, dispose, or a
            // retarget must never write back into loader state.
            AsyncOperationHandle<GameObject> handle = default;
            try
            {
                handle = _operationAdapter.InstantiateAsync(
                    BuildAddress(chapterId), parent);
                GameObject instance = await handle.ToUniTask(
                    cancellationToken: operationToken);
                WorkshopRoomPresenter presenter = instance == null
                    ? null
                    : instance.GetComponent<WorkshopRoomPresenter>();
                if (presenter == null)
                {
                    throw new InvalidOperationException(
                        "The workshop room prefab has no " +
                        "WorkshopRoomPresenter.");
                }

                if (cancellationOwner.IsCancellationRequested ||
                    _disposed ||
                    generation != _loadGeneration)
                {
                    operationToken.ThrowIfCancellationRequested();
                    throw new ObjectDisposedException(
                        nameof(AddressableWorkshopRoomLoader));
                }

                _handle = handle;
                _hasHandle = true;
                handle = default;
                _presenter = presenter;
                _chapterId = chapterId;
                _parent = parent;
                return presenter;
            }
            catch (OperationCanceledException)
            {
                // Addressables keeps running after lifecycle cancellation, so
                // release the operation-owned late instance exactly once.
                await ReleaseLateAsync(handle);
                handle = default;
                throw;
            }
            catch
            {
                ReleaseHandle(ref handle);
                throw;
            }
            finally
            {
                CompleteLoadAttempt(generation, cancellationOwner);
            }
        }

        private void CompleteLoadAttempt(
            int generation,
            CancellationTokenSource cancellationOwner)
        {
            if (generation == _loadGeneration)
            {
                _loading = false;
            }

            if (!ReferenceEquals(_loadCancellation, cancellationOwner))
            {
                return;
            }

            _loadCancellation = null;
            cancellationOwner.Dispose();
        }

        public UniTask UnloadAsync(CancellationToken cancellationToken)
        {
            ReleaseCurrent();
            cancellationToken.ThrowIfCancellationRequested();
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
                CancellationTokenSource cancellationOwner =
                    _loadCancellation;
                _loadCancellation = null;
                cancellationOwner.Cancel();
                cancellationOwner.Dispose();
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

        private async UniTask ReleaseLateAsync(
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

        private void ReleaseHandle(
            ref AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            AsyncOperationHandle<GameObject> released = handle;
            handle = default;
            _operationAdapter.ReleaseInstance(released);
        }
    }
}
