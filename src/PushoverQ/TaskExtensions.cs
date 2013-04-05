﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    static class TaskExtensions
    {
        private static readonly TaskCompletionSource<object> NeverCompleteSource = new TaskCompletionSource<object>();
        public static Task NeverComplete { get { return NeverCompleteSource.Task; } }

        public static Task IgnoreExceptions(this Task task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        public static Task<T> IgnoreExceptions<T>(this Task<T> task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        public static Task ToTask(this CancellationToken token)
        {
            if (!token.CanBeCanceled) return NeverComplete;

            var tcs = new TaskCompletionSource<object>();
            token.Register(tcs.SetCanceled);
            return tcs.Task;
        }

        internal class NaiveTimeoutException : TimeoutException
        {
            public NaiveTimeoutException() : base()
            {
            }

            public NaiveTimeoutException(string message)
                : base(message)
            {
                
            }

            public NaiveTimeoutException(string message, Exception innerException)
                : base(message, innerException)
            {

            }
        }

        public static Task TimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout == TimeSpan.FromMilliseconds(-1))
                return NeverComplete;
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout", "Invalid timeout");

            var tcs = new TaskCompletionSource<object>();
            var ctr = new CancellationTokenRegistration();
            var timer = new Timer(self =>
            {
                ctr.Dispose();
                ((Timer)self).Dispose();
                tcs.TrySetException(new NaiveTimeoutException());
            });

            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(() =>
                {
                    timer.Dispose();
                    tcs.TrySetCanceled();
                });
            }

            timer.Change(timeout, TimeSpan.FromMilliseconds(-1));
            return tcs.Task;
        }

        /// <summary>
        /// A naive implementation of timeout and cancellation over an uncancellable <see cref="Task"/>.
        /// </summary>
        /// <typeparam name="T">The result type of the task</typeparam>
        /// <param name="task">the uncancellable task</param>
        /// <param name="timeout">timeout after which to give up</param>
        /// <param name="token">token to monitor for cancellation</param>
        /// <returns></returns>
        public static async Task<T> WithTimeoutAndCancellation<T>(this Task<T> task, TimeSpan timeout, CancellationToken token)
        {
#pragma warning disable 4014
            task.IgnoreExceptions();

            var timeoutTask = TimeoutAsync(timeout, CancellationToken.None);
            var cancelTask = token.ToTask();

            timeoutTask.IgnoreExceptions();
            cancelTask.IgnoreExceptions();
#pragma warning restore 4014

            await Task.WhenAny(task, timeoutTask, cancelTask);
            return task.Result;
        }

        /// <summary>
        /// A naive implementation of timeout and cancellation over an uncancellable <see cref="Task"/>.
        /// </summary>
        /// <param name="task">the uncancellable task</param>
        /// <param name="timeout">timeout after which to give up</param>
        /// <param name="token">token to monitor for cancellation</param>
        /// <returns></returns>
        public static async Task WithTimeoutAndCancellation(this Task task, TimeSpan timeout, CancellationToken token)
        {
#pragma warning disable 4014
            task.IgnoreExceptions();

            var timeoutTask = TimeoutAsync(timeout, CancellationToken.None);
            var cancelTask = token.ToTask();

            timeoutTask.IgnoreExceptions();
            cancelTask.IgnoreExceptions();
#pragma warning restore 4014

            await Task.WhenAny(task, timeoutTask, cancelTask);
        }

        public static Task<T> AsTask<T>(this Exception e)
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetException(e);
            return tcs.Task;
        }

        public static Task AsTask(this Exception e)
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(e);
            return tcs.Task;
        }
    }
}
