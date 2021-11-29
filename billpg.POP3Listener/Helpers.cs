/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace billpg.pop3
{
    internal static class Helpers
    {
        /// <summary>
        /// Lookup table of ObjectDisposeException.ObjectName property values we want to ignore as errors.
        /// </summary>
        private static readonly HashSet<string> allowedDisposedObjectNames
            = new HashSet<string> { "System.Net.Sockets.Socket", "System.Net.Sockets.NetworkStream" };

        /// <summary>
        /// Call a supplied async function, catching and ignoring known exceptions that occur on shutdown.
        /// </summary>
        /// <param name="fn">Function to call inside a try/catch.</param>
        internal static void TryCallCatch(Action fn)
        {
            try
            {
                /* Call the supplied function inside the try block.. */
                fn();
            }

            /* Ignore these two exceptions, known to be thrown during socket shutdown.
             * Other exceptions are allowed to fall to the caller. */
            catch (ObjectDisposedException ex)
            when (allowedDisposedObjectNames.Contains(ex.ObjectName))
            { }
            catch (InvalidOperationException ex)
            when (ex.Message.Contains("Start()"))
            { }
        }

        internal static void DoNothingAction()
        {
            /* Do nothing. As designed. */
        }

        internal static string AsDotQuoted(string line)
        {
            /* If the line starts with a ".", add another. */
            if (line.Length > 1 && line[0] == '.')
                return "." + line;
            else
                return line;
        }

        internal static System.Threading.Tasks.Task WriteLineAsync(this System.IO.Stream str, string text)
        {
            byte[] lineAsBytes = Encoding.ASCII.GetBytes(text + "\r\n");
            return str.WriteAsync(lineAsBytes, 0, lineAsBytes.Length);
        }

        internal static bool IsConnectionClosed(this System.Threading.Tasks.Task<int> task)
        {
            /* Success but returned zero, closed connection. */
            if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result == 0)
                return true;

            /* If faulted, check if exceptions are the known ones. */
            if (task.Status == System.Threading.Tasks.TaskStatus.Faulted && TestPerException(task.Exception))
                return true;

            /* Not a socket-closed condition. */
            return false;

            /* Test if exception indicates a closed connection. */
            bool TestPerException(Exception ex)
            {
                /* Handle Aggregate exceptions. */
                if (ex is AggregateException agex)
                    return agex.InnerExceptions.Any(TestPerException);

                /* Socket IO exception? -2146232800 is "Connection closed by remote." */
                if (ex is System.IO.IOException ioex && ioex.HResult == -2146232800)
                    return true;

                /* None of these. */
                return false;
            }
        }

        internal static void LockContinueWith(this System.Threading.Tasks.Task task, object mutex, Action<System.Threading.Tasks.Task> fn)
        {
            task.ContinueWith(Internal);
            void Internal(System.Threading.Tasks.Task taskInner)
            {
                lock(mutex)
                {
                    fn(taskInner);
                }
            }
        }


        internal static void LockContinueWith<T>(this System.Threading.Tasks.Task<T> task, object mutex, Action<System.Threading.Tasks.Task<T>> fn)
        {
            task.ContinueWith(Internal);
            void Internal(System.Threading.Tasks.Task<T> taskInner)
            {
                lock (mutex)
                {
                    fn(taskInner);
                }
            }
        }
    }
}
