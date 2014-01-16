using System;
using System.Threading;
using System.Threading.Tasks;

namespace PushoverQ
{
    internal static partial class TaskExtensions
    {
        private static readonly TaskCompletionSource<object> NeverCompleteSource = new TaskCompletionSource<object>();

        public static Task NeverComplete { get { return NeverCompleteSource.Task; } }

        /// <summary>
        /// Run a task with a timeout.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The maximum amount of time the task is allowed to run.</param>
        /// <exception cref="TimeoutException">A timeout exception if the task takes too long to run.</exception>
        /// <returns>A <see cref="Task"/>.</returns>
        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            if (task != await TaskEx.WhenAny(task, TaskEx.Delay(timeout)))
                throw new NaiveTimeoutException();

            await task;
        }

        /// <summary>
        /// Run a task with a timeout.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="timeout">The maximum amount of time in milliseconds the task is allowed to run.</param>
        /// <exception cref="TimeoutException">A timeout exception if the task takes too long to run. </exception>
        /// <returns>A <see cref="Task"/>.</returns>
        public static async Task WithTimeout(this Task task, int timeout)
        {
            await WithTimeout(task, TimeSpan.FromMilliseconds(timeout));
        }

        /// <summary>
        /// A naive implementation of timeout and cancellation over an uncancelable <see cref="Task"/>.
        /// </summary>
        /// <typeparam name="T">The result type of the task</typeparam>
        /// <param name="task">the uncancelable task</param>
        /// <param name="token">token to monitor for cancellation</param>
        /// <returns>The <see cref="Task"/> with a <see cref="CancellationToken"/>.</returns>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
        {
#pragma warning disable 4014
            task.IgnoreExceptions();

            var cancelTask = token.ToTask();

            cancelTask.IgnoreExceptions();
#pragma warning restore 4014

            await TaskEx.WhenAny(task, cancelTask);
            return task.Result;
        }

        /// <summary>
        /// A naive implementation of timeout and cancellation over an uncancelable <see cref="Task"/>.
        /// </summary>
        /// <param name="task">the uncancelable task</param>
        /// <param name="token">token to monitor for cancellation</param>
        /// <returns>The <see cref="Task"/> with a <see cref="CancellationToken"/>.</returns>
        public static async Task WithCancellation(this Task task, CancellationToken token)
        {
#pragma warning disable 4014
            task.IgnoreExceptions();

            var cancelTask = token.ToTask();

            cancelTask.IgnoreExceptions();
#pragma warning restore 4014

            await TaskEx.WhenAny(task, cancelTask);
        }
        
        /// <summary>
        /// Run a task as void, allowing control to return immediately to the application.
        /// </summary>
        /// <param name="task">The task to run.</param>
        public static async void ToVoid(this Task task)
        {
            await task;
        }

        /// <summary>
        /// Executes the <see cref="Task"/> ignoring all thrown exceptions during execution.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> to run.</param>
        /// <returns>A <see cref="Task"/> that ignores all thrown execeptions during execution.</returns>
        public static Task IgnoreExceptions(this Task task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        /// <summary>
        /// Executes the <see cref="Task"/> ignoring all thrown exceptions during execution.
        /// </summary>
        /// <typeparam name="T">The return type of the <see cref="Task"/>.</typeparam>
        /// <param name="task">The <see cref="Task"/> to run.</param>
        /// <returns>A <see cref="Task"/> that ignores all thrown execeptions during execution.</returns>
        public static Task<T> IgnoreExceptions<T>(this Task<T> task)
        {
            task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        /// <summary>
        /// Creates a task from the <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="token">The <see cref="CancellationToken"/> to use.</param>
        /// <returns>A <see cref="Task"/></returns>
        public static Task ToTask(this CancellationToken token)
        {
            if (!token.CanBeCanceled) return NeverComplete;

            var tcs = new TaskCompletionSource<object>();
            token.Register(tcs.SetCanceled);
            return tcs.Task;
        }

        /// <summary>
        /// Converts an exception to a task for throwing an exception from a non-async func.
        /// </summary>
        /// <typeparam name="T">The return type of the <see cref="Task"/>.</typeparam>
        /// <param name="e">The <see cref="Exception"/> to throw.</param>
        /// <returns>A <see cref="Task"/> with the <see cref="e"/> set as the <see cref="Exception"/>.</returns>
        public static Task<T> AsTask<T>(this Exception e)
        {
            var tcs = new TaskCompletionSource<T>();
            tcs.SetException(e);
            return tcs.Task;
        }

        /// <summary>
        /// Converts an exception to a task for throwing an exception from a non-async func.
        /// </summary>
        /// <param name="e">The <see cref="Exception"/> to throw.</param>
        /// <returns>A <see cref="Task"/> with the <see cref="e"/> set as the <see cref="Exception"/>.</returns>
        public static Task AsTask(this Exception e)
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(e);
            return tcs.Task;
        }
    }
}
