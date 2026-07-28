using System;
using System.Threading;
using CalmSpace.Core;
using CalmSpace.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace CalmSpace.Levels
{
    public enum LevelState
    {
        Uninitialized = 0,
        Initializing = 1,
        Active = 2,
        Completing = 3,
        Completed = 4
    }

    /// <summary>
    /// Common lifecycle for all authored sorting, cleaning, screw-puzzle, and
    /// fitting levels.
    /// </summary>
    public abstract class LevelBase : MonoBehaviour
    {
        [SerializeField]
        private LevelDefinition _definition;

        private IPresentationActivityCoordinator _activityCoordinator;
        private IDisposable _gameplayLease;
        private ItemSnapController[] _items;
        private LevelProgressTracker _progress;
        private bool _destroying;

        public event Action<LevelBase> LevelCompleted;

        public event Action<LevelState> StateChanged;

        public event Action<int, int> ProgressChanged;

        public LevelDefinition Definition => _definition;

        public LevelState State { get; private set; } =
            LevelState.Uninitialized;

        public int PlacedItemCount => _progress?.PlacedCount ?? 0;

        public int ItemCount => _items?.Length ?? 0;

        public abstract LevelType SupportedType { get; }

        [Inject]
        public void Construct(
            IPresentationActivityCoordinator activityCoordinator)
        {
            if (activityCoordinator == null)
            {
                throw new ArgumentNullException(
                    nameof(activityCoordinator));
            }

            if (_activityCoordinator == activityCoordinator)
            {
                return;
            }

            if (_gameplayLease != null)
            {
                throw new InvalidOperationException(
                    "Cannot replace the activity coordinator while active.");
            }

            _activityCoordinator = activityCoordinator;
        }

        public bool InitLevel()
        {
            return InitLevel(_definition);
        }

        public bool InitLevel(LevelDefinition definition)
        {
            if (State != LevelState.Uninitialized ||
                definition == null ||
                _activityCoordinator == null ||
                definition.Type != SupportedType)
            {
                return false;
            }

            SetState(LevelState.Initializing);
            _definition = definition;

            try
            {
                _items = GetComponentsInChildren<ItemSnapController>(true);
                _progress = new LevelProgressTracker(_items.Length);

                for (var index = 0; index < _items.Length; index++)
                {
                    var item = _items[index];
                    if (item == null ||
                        !_progress.RegisterItem(item.ItemInstanceId))
                    {
                        throw new InvalidOperationException(
                            "A level item could not be registered.");
                    }

                    item.Placed += HandleItemPlaced;
                    if (item.IsPlaced)
                    {
                        _progress.TryMarkPlaced(item.ItemInstanceId);
                    }
                }

                if (isActiveAndEnabled && !TryAcquireGameplayLease())
                {
                    RollBackInitialization();
                    return false;
                }

                OnLevelInitialized();
                SetState(LevelState.Active);
                InvokeProgressChanged();

                if (CheckWinCondition())
                {
                    CompleteLevel().Forget();
                }

                return true;
            }
            catch
            {
                RollBackInitialization();
                throw;
            }
        }

        public bool OnItemPlaced(ItemSnapController item)
        {
            return item != null && OnItemPlaced(item.ItemInstanceId);
        }

        public bool OnItemPlaced(int itemInstanceId)
        {
            if (State != LevelState.Active ||
                _progress == null ||
                !_progress.TryMarkPlaced(itemInstanceId))
            {
                return false;
            }

            try
            {
                OnUniqueItemPlaced(itemInstanceId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (CheckWinCondition())
            {
                CompleteLevel().Forget();
            }

            InvokeProgressChanged();
            return true;
        }

        public virtual bool CheckWinCondition()
        {
            return _progress != null && _progress.IsComplete;
        }

        public UniTask CompleteLevel(
            CancellationToken cancellationToken = default)
        {
            if (State != LevelState.Active || !CheckWinCondition())
            {
                return UniTask.CompletedTask;
            }

            SetState(LevelState.Completing);
            ReleaseGameplayLease();
            return CompleteLevelCoreAsync(cancellationToken);
        }

        protected virtual void OnLevelInitialized()
        {
        }

        protected virtual void OnUniqueItemPlaced(int itemInstanceId)
        {
        }

        protected virtual UniTask PlayCompletionEffectsAsync(
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        protected virtual void OnLevelCompletionFinished()
        {
        }

        protected virtual void OnEnable()
        {
            if (State == LevelState.Active)
            {
                if (!TryAcquireGameplayLease())
                {
                    Debug.LogWarning(
                        "Level activation was deferred because a fullscreen " +
                        "presentation owns the activity gate.",
                        this);
                    gameObject.SetActive(false);
                }
            }
        }

        protected virtual void OnDisable()
        {
            ReleaseGameplayLease();
        }

        protected virtual void OnDestroy()
        {
            _destroying = true;
            ReleaseGameplayLease();
            UnsubscribeItems();
        }

        private async UniTask CompleteLevelCoreAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using (var linkedCancellation =
                       CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken,
                           this.GetCancellationTokenOnDestroy()))
                {
                    await PlayCompletionEffectsAsync(
                        linkedCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Logical completion remains valid if presentation is skipped.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            if (_destroying || this == null)
            {
                return;
            }

            SetState(LevelState.Completed);
            try
            {
                OnLevelCompletionFinished();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            InvokeLevelCompleted();
        }

        private void HandleItemPlaced(ItemSnapController item)
        {
            OnItemPlaced(item);
        }

        private bool TryAcquireGameplayLease()
        {
            if (_gameplayLease != null)
            {
                return true;
            }

            if (_activityCoordinator == null)
            {
                return false;
            }

            return _activityCoordinator.TryEnterGameplay(
                out _gameplayLease);
        }

        private void ReleaseGameplayLease()
        {
            var lease = _gameplayLease;
            _gameplayLease = null;
            lease?.Dispose();
        }

        private void RollBackInitialization()
        {
            ReleaseGameplayLease();
            UnsubscribeItems();
            _items = null;
            _progress = null;
            SetState(LevelState.Uninitialized);
        }

        private void UnsubscribeItems()
        {
            if (_items == null)
            {
                return;
            }

            for (var index = 0; index < _items.Length; index++)
            {
                if (_items[index] != null)
                {
                    _items[index].Placed -= HandleItemPlaced;
                }
            }
        }

        private void SetState(LevelState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            var handler = StateChanged;
            if (handler == null)
            {
                return;
            }

            foreach (Action<LevelState> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(state);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void InvokeProgressChanged()
        {
            var handler = ProgressChanged;
            if (handler == null)
            {
                return;
            }

            var placedCount = _progress?.PlacedCount ?? 0;
            var itemCount = _items?.Length ?? 0;
            foreach (Action<int, int> subscriber in
                     handler.GetInvocationList())
            {
                try
                {
                    subscriber(placedCount, itemCount);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void InvokeLevelCompleted()
        {
            var handler = LevelCompleted;
            if (handler == null)
            {
                return;
            }

            foreach (Action<LevelBase> subscriber in
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
