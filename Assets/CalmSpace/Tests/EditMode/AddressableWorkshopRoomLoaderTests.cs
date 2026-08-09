using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CalmSpace.UI;
using CalmSpace.Workshop;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CalmSpace.Tests.EditMode
{
    public sealed class AddressableWorkshopRoomLoaderTests
    {
        private GameObject _parentObject;
        private ControlledOperationAdapter _adapter;
        private AddressableWorkshopRoomLoader _loader;

        [TearDown]
        public void TearDown()
        {
            _loader?.Dispose();
            _adapter?.Dispose();
            if (_parentObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_parentObject);
            }
        }

        [Test]
        public async Task EachSharedWaiterObservesItsOwnCancellation()
        {
            _adapter = new ControlledOperationAdapter();
            _loader = new AddressableWorkshopRoomLoader(_adapter);
            _parentObject = new GameObject("Workshop room parent");
            Task<WorkshopRoomPresenter> physicalLoad = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();

            using var waiterCancellation = new CancellationTokenSource();
            Task<WorkshopRoomPresenter> waiter = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    waiterCancellation.Token)
                .AsTask();

            waiterCancellation.Cancel();
            Task settled = await Task.WhenAny(waiter, Task.Delay(250));

            Assert.That(
                settled,
                Is.SameAs(waiter),
                "A cancelled joiner must settle without cancelling the " +
                "shared physical load.");
            Assert.That(
                await CaptureFailure(waiter),
                Is.InstanceOf<OperationCanceledException>());
            Assert.That(physicalLoad.IsCompleted, Is.False);
            Assert.That(_adapter.StartCount, Is.EqualTo(1));

            GameObject room = CreateRoom();
            _adapter.LastOperation.Succeed(room);

            Assert.That(await physicalLoad, Is.SameAs(
                room.GetComponent<WorkshopRoomPresenter>()));
            await _loader.UnloadAsync(CancellationToken.None);
            Assert.That(_adapter.ReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public async Task CancellingOneWaiterDoesNotCancelTheSharedLoad()
        {
            CreateLoader();
            using var firstCancellation = new CancellationTokenSource();
            Task<WorkshopRoomPresenter> first = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    firstCancellation.Token)
                .AsTask();
            Task<WorkshopRoomPresenter> second = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();

            firstCancellation.Cancel();
            await AssertCancelled(first);

            Assert.That(second.IsCompleted, Is.False);
            Assert.That(_adapter.StartCount, Is.EqualTo(1));
            Assert.That(_adapter.ReleaseCount, Is.Zero);

            GameObject room = CreateRoom();
            _adapter.LastOperation.Succeed(room);

            Assert.That(await second, Is.SameAs(
                room.GetComponent<WorkshopRoomPresenter>()));
            Assert.That(_loader.IsLoaded, Is.True);
            Assert.That(_adapter.ReleaseCount, Is.Zero);
        }

        [Test]
        public async Task SuccessfulAndFailedAttemptsReleaseTheirCancellationOwner()
        {
            CreateLoader();
            Task<WorkshopRoomPresenter> successful = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();
            CancellationTokenSource successfulOwner = GetLoadCancellation();
            _adapter.LastOperation.Succeed(CreateRoom());

            await successful;
            bool successCleared = GetLoadCancellation() == null;
            bool successDisposed = IsDisposed(successfulOwner);
            await _loader.UnloadAsync(CancellationToken.None);

            Task<WorkshopRoomPresenter> failed = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();
            CancellationTokenSource failedOwner = GetLoadCancellation();
            _adapter.LastOperation.Fail(
                new InvalidOperationException("Controlled load failure."));
            _adapter.Pump();

            Exception failure = await CaptureFailure(failed);
            bool failureCleared = GetLoadCancellation() == null;
            bool failureDisposed = IsDisposed(failedOwner);

            Assert.That(successCleared, Is.True);
            Assert.That(successDisposed, Is.True);
            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(failureCleared, Is.True);
            Assert.That(failureDisposed, Is.True);
            Assert.That(_adapter.ReleaseCount, Is.EqualTo(2));
        }

        [Test]
        public async Task CancelledUnloadStillReleasesTheOwnedRoom()
        {
            CreateLoader();
            Task<WorkshopRoomPresenter> load = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();
            _adapter.LastOperation.Succeed(CreateRoom());
            await load;
            using var unloadCancellation = new CancellationTokenSource();
            unloadCancellation.Cancel();

            Exception cancellation = await CaptureFailure(
                () => _loader.UnloadAsync(unloadCancellation.Token).AsTask());

            Assert.That(cancellation, Is.TypeOf<OperationCanceledException>());
            Assert.That(_loader.IsLoaded, Is.False);
            Assert.That(_loader.Presenter, Is.Null);
            Assert.That(_adapter.ReleaseCount, Is.EqualTo(1));
            Assert.That(_adapter.LastOperation.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DisposeDuringLoadReleasesALateInstanceExactlyOnce()
        {
            CreateLoader();
            Task<WorkshopRoomPresenter> load = _loader.LoadAsync(
                    "chapter-a",
                    _parentObject.transform,
                    CancellationToken.None)
                .AsTask();
            ControlledOperation operation = _adapter.LastOperation;

            _loader.Dispose();
            operation.Succeed(CreateRoom());

            Exception cancellation = await CaptureFailure(load);
            _loader.Dispose();

            Assert.That(cancellation, Is.TypeOf<OperationCanceledException>());
            Assert.That(_loader.IsLoaded, Is.False);
            Assert.That(_adapter.ReleaseCount, Is.EqualTo(1));
            Assert.That(operation.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public async Task SynchronousOperationStartFailureClearsStateAndAllowsRetry()
        {
            CreateLoader();
            _adapter.StartException =
                new InvalidOperationException("Controlled start failure.");

            Exception failure = await CaptureFailure(
                () => _loader.LoadAsync(
                        "chapter-a",
                        _parentObject.transform,
                        CancellationToken.None)
                    .AsTask());
            UniTask<WorkshopRoomPresenter> retry = _loader.LoadAsync(
                "chapter-a",
                _parentObject.transform,
                CancellationToken.None);

            Assert.That(failure, Is.TypeOf<InvalidOperationException>());
            Assert.That(_adapter.StartCount, Is.EqualTo(2));

            GameObject room = CreateRoom();
            _adapter.LastOperation.Succeed(room);

            Assert.That(await retry, Is.SameAs(
                room.GetComponent<WorkshopRoomPresenter>()));
            Assert.That(_loader.IsLoaded, Is.True);
        }

        private void CreateLoader()
        {
            _adapter = new ControlledOperationAdapter();
            _loader = new AddressableWorkshopRoomLoader(_adapter);
            _parentObject = new GameObject("Workshop room parent");
        }

        private static GameObject CreateRoom()
        {
            var room = new GameObject("Controlled workshop room");
            room.AddComponent<WorkshopRoomPresenter>();
            return room;
        }

        private CancellationTokenSource GetLoadCancellation()
        {
            FieldInfo field = typeof(AddressableWorkshopRoomLoader).GetField(
                "_loadCancellation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (CancellationTokenSource)field.GetValue(_loader);
        }

        private static bool IsDisposed(CancellationTokenSource source)
        {
            try
            {
                _ = source.Token;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        private static async Task AssertCancelled(Task task)
        {
            Task settled = await Task.WhenAny(task, Task.Delay(250));
            Assert.That(settled, Is.SameAs(task));
            Assert.That(
                await CaptureFailure(task),
                Is.InstanceOf<OperationCanceledException>());
        }

        private static async Task<Exception> CaptureFailure(Task task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static async Task<Exception> CaptureFailure(
            Func<Task> operation)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private sealed class ControlledOperationAdapter :
            IWorkshopRoomOperationAdapter,
            IDisposable
        {
            private readonly ResourceManager _resourceManager =
                new ResourceManager();
            private readonly List<AsyncOperationHandle<GameObject>> _handles =
                new List<AsyncOperationHandle<GameObject>>();

            public int StartCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public ControlledOperation LastOperation { get; private set; }
            public AsyncOperationHandle<GameObject> LastHandle { get; private set; }
            public Exception StartException { get; set; }

            public AsyncOperationHandle<GameObject> InstantiateAsync(
                string address,
                Transform parent)
            {
                StartCount++;
                if (StartException != null)
                {
                    Exception exception = StartException;
                    StartException = null;
                    throw exception;
                }

                LastOperation = new ControlledOperation();
                LastHandle = _resourceManager.StartOperation(
                    LastOperation,
                    default(AsyncOperationHandle));
                _handles.Add(LastHandle);
                return LastHandle;
            }

            public void ReleaseInstance(
                AsyncOperationHandle<GameObject> handle)
            {
                ReleaseCount++;
                _resourceManager.Release(handle);
            }

            public void Pump()
            {
                LastHandle.WaitForCompletion();
            }

            public void Dispose()
            {
                for (var index = 0; index < _handles.Count; index++)
                {
                    AsyncOperationHandle<GameObject> handle = _handles[index];
                    if (handle.IsValid())
                    {
                        _resourceManager.Release(handle);
                    }
                }

                _resourceManager.Dispose();
            }
        }

        private sealed class ControlledOperation :
            AsyncOperationBase<GameObject>
        {
            public int DestroyCount { get; private set; }

            public void Succeed(GameObject room)
            {
                Complete(room, true, string.Empty);
            }

            public void Fail(Exception exception)
            {
                Complete(null, false, exception);
            }

            protected override void Execute()
            {
            }

            protected override void Destroy()
            {
                DestroyCount++;
                if (Result != null)
                {
                    UnityEngine.Object.DestroyImmediate(Result);
                }
            }
        }
    }
}
