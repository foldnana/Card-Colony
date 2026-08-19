using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Owns temporary narrative state and releases it exactly once in reverse
    /// acquisition order, including when one cleanup action throws.
    /// </summary>
    public sealed class NarrativeCleanupScope : IDisposable
    {
        private readonly Stack<Action> cleanupActions = new();
        private bool disposed;

        public bool IsDisposed => disposed;

        public void Push(Action cleanup)
        {
            if (cleanup == null)
                return;
            if (disposed)
            {
                cleanup();
                return;
            }
            cleanupActions.Push(cleanup);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Exception firstFailure = null;
            while (cleanupActions.Count > 0)
            {
                try
                {
                    cleanupActions.Pop().Invoke();
                }
                catch (Exception exception)
                {
                    firstFailure ??= exception;
                }
            }
            if (firstFailure != null)
                throw firstFailure;
        }
    }
}
