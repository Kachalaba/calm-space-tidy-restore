using System;
using System.Threading;

namespace CalmSpace.Core
{
    public enum AdBlockReason
    {
        None = 0,
        GameplayActive = 1,
        DragActive = 2,
        AdInProgress = 3,
        Cooldown = 4,
        EntitlementSuppressed = 5,
        ProviderNotReady = 6,
        NotInitialized = 7,
        Cancelled = 8,
        InvalidRequest = 9
    }

    public interface IPresentationActivityCoordinator
    {
        int ActiveGameplayCount { get; }

        int ActiveDragCount { get; }

        bool IsAdActive { get; }

        bool TryEnterGameplay(out IDisposable lease);

        bool TryEnterDrag(out IDisposable lease);

        bool TryEnterAd(
            out IDisposable lease,
            out AdBlockReason blockReason);
    }

    /// <summary>
    /// Atomically coordinates mutually exclusive full-screen presentation with
    /// any number of gameplay and drag owners.
    /// </summary>
    public sealed class PresentationActivityCoordinator :
        IPresentationActivityCoordinator
    {
        private readonly object _sync = new object();
        private int _activeGameplayCount;
        private int _activeDragCount;
        private bool _isAdActive;

        public int ActiveGameplayCount
        {
            get
            {
                lock (_sync)
                {
                    return _activeGameplayCount;
                }
            }
        }

        public int ActiveDragCount
        {
            get
            {
                lock (_sync)
                {
                    return _activeDragCount;
                }
            }
        }

        public bool IsAdActive
        {
            get
            {
                lock (_sync)
                {
                    return _isAdActive;
                }
            }
        }

        public bool TryEnterGameplay(out IDisposable lease)
        {
            lock (_sync)
            {
                if (_isAdActive)
                {
                    lease = null;
                    return false;
                }

                checked
                {
                    _activeGameplayCount++;
                }

                lease = new ActivityLease(ReleaseGameplay);
                return true;
            }
        }

        public bool TryEnterDrag(out IDisposable lease)
        {
            lock (_sync)
            {
                if (_isAdActive)
                {
                    lease = null;
                    return false;
                }

                checked
                {
                    _activeDragCount++;
                }

                lease = new ActivityLease(ReleaseDrag);
                return true;
            }
        }

        public bool TryEnterAd(
            out IDisposable lease,
            out AdBlockReason blockReason)
        {
            lock (_sync)
            {
                if (_isAdActive)
                {
                    lease = null;
                    blockReason = AdBlockReason.AdInProgress;
                    return false;
                }

                if (_activeGameplayCount > 0)
                {
                    lease = null;
                    blockReason = AdBlockReason.GameplayActive;
                    return false;
                }

                if (_activeDragCount > 0)
                {
                    lease = null;
                    blockReason = AdBlockReason.DragActive;
                    return false;
                }

                _isAdActive = true;
                lease = new ActivityLease(ReleaseAd);
                blockReason = AdBlockReason.None;
                return true;
            }
        }

        private void ReleaseGameplay()
        {
            lock (_sync)
            {
                if (_activeGameplayCount > 0)
                {
                    _activeGameplayCount--;
                }
            }
        }

        private void ReleaseDrag()
        {
            lock (_sync)
            {
                if (_activeDragCount > 0)
                {
                    _activeDragCount--;
                }
            }
        }

        private void ReleaseAd()
        {
            lock (_sync)
            {
                _isAdActive = false;
            }
        }

        private sealed class ActivityLease : IDisposable
        {
            private Action _release;

            public ActivityLease(Action release)
            {
                _release = release ??
                    throw new ArgumentNullException(nameof(release));
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _release, null)?.Invoke();
            }
        }
    }
}
