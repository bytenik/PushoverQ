using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// An async timer.
    /// </summary>
    class AsyncTimer : IDisposable
    {
        private readonly Func<CancellationToken, Task> _action;
        private CancellationTokenSource _cts;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTimer"/> class.
        /// </summary>
        /// <param name="action">The action to perform when the timer elapses.</param>
        public AsyncTimer(Func<CancellationToken, Task> action)
        {
            _action = action;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTimer"/> class.
        /// </summary>
        /// <param name="action">The action to perform when the timer elapses.</param>
        /// <param name="interval">The amount of time that should pass between runs.</param>
        public AsyncTimer(Func<CancellationToken, Task> action, TimeSpan interval)
        {
            _action = action;

            Change(interval, interval);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTimer"/> class.
        /// </summary>
        /// <param name="action">The action to perform when the timer elapses.</param>
        /// <param name="dueTime">The amount of time before the action should be performed initially.</param>
        /// <param name="interval">The amount of time that should pass between runs.</param>
        public AsyncTimer(Func<CancellationToken, Task> action, TimeSpan dueTime, TimeSpan interval)
        {
            _action = action;

            Change(dueTime, interval);
        }

        /// <summary>
        /// Changes the due time and interval.
        /// </summary>
        /// <param name="dueTime">The amount of time before the action should be performed initially.</param>
        /// <param name="interval">The amount of time that should pass between runs.</param>
        public void Change(TimeSpan dueTime, TimeSpan interval)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (dueTime == Timeout.InfiniteTimeSpan && interval == Timeout.InfiniteTimeSpan)
                return;
            if (dueTime == Timeout.InfiniteTimeSpan)
                dueTime = interval;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Run(async () =>
            {
                await Task.Delay(dueTime, token);

                while (!token.IsCancellationRequested)
                {
                    await _action(token);
                    await Task.Delay(interval, token);
                }

                throw new OperationCanceledException(token);
            },
                _cts.Token);
        }

        /// <summary>
        /// Stops the timer, if it is already running.
        /// </summary>
        public void Stop()
        {
            Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        ~AsyncTimer()
        {
            Dispose();
        }
    }
}
