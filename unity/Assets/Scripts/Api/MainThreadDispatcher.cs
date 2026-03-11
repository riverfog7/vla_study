using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace VlaStudy.UnityHarness.Api
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();
        private int _mainThreadId;

        private void Awake()
        {
            _mainThreadId = Environment.CurrentManagedThreadId;
        }

        private void Update()
        {
            while (_pendingActions.TryDequeue(out var action))
            {
                action();
            }
        }

        public Task EnqueueAsync(Action action)
        {
            if (Environment.CurrentManagedThreadId == _mainThreadId)
            {
                action();
                return Task.CompletedTask;
            }

            var taskSource = new TaskCompletionSource<bool>();
            _pendingActions.Enqueue(() =>
            {
                try
                {
                    action();
                    taskSource.SetResult(true);
                }
                catch (Exception exception)
                {
                    taskSource.SetException(exception);
                }
            });

            return taskSource.Task;
        }

        public Task<T> EnqueueAsync<T>(Func<T> func)
        {
            if (Environment.CurrentManagedThreadId == _mainThreadId)
            {
                return Task.FromResult(func());
            }

            var taskSource = new TaskCompletionSource<T>();
            _pendingActions.Enqueue(() =>
            {
                try
                {
                    taskSource.SetResult(func());
                }
                catch (Exception exception)
                {
                    taskSource.SetException(exception);
                }
            });

            return taskSource.Task;
        }
    }
}
