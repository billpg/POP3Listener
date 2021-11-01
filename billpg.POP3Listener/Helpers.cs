using System;
using System.Collections.Generic;
using System.Text;

namespace billpg.pop3
{
    internal static class Helpers
    {
        /// <summary>
        /// Call a supplied async function, catching and ignoring known exceptions that occur on shutdown.
        /// </summary>
        /// <param name="fn"></param>
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
            when (ex.ObjectName == "System.Net.Sockets.Socket")
            { }
            catch (InvalidOperationException ex)
            when (ex.Message.Contains("Start()"))
            { }
        }
    }
}
