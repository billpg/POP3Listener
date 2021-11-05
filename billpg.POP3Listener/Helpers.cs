using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
