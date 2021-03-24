/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Text;

namespace billpg.pop3svc
{
    internal static class ContentWrappers
    {
        /// <summary>
        /// Wrap a message content interface with a NextLineFn function suiatbe for a TOP command.
        /// </summary>
        /// <param name="msg">Provider's message content object.</param>
        /// <param name="lineCount">Numbe rof lines after the ehader.</param>
        /// <returns>Wrapper function.</returns>
        internal static NextLineFn WrapForTop(IMessageContent msg, long lineCount)
        {
            /* Update counter, because the blank line doesn't count. */
            lineCount += 1;

            /* Raise a flag if we're inside the message bosy. */
            bool insideMesageBody = false;

            /* Raise a flag once Close has been called. */
            bool calledClose = false;
            void CloseStream()
            {
                /* Notthing to do if closed already called. */
                if (calledClose)
                    return;

                /* Inform the message content object. */
                msg.Close();

                /* Raise the flag. */
                calledClose = true;
            }

            /* Return he wrapper function. */
            return Impl;
            string Impl()
            {
                /* Have we exhausted the requested line count? */
                if (lineCount == 0)
                {
                    /* Inform the content object and raise flag. */
                    CloseStream();

                    /* Signal end-of-stream. */
                    return null;
                }

                /* Load a line. Test for end-of-message. */
                string line = msg.NextLine();
                if (line == null)
                {
                    /* Close the message content stream and return null. */
                    CloseStream();
                    return null;
                }

                /* Is it the blank line that ends the header? */
                if (insideMesageBody == false && line.Length == 0)
                    insideMesageBody = true;

                /* Lower the counter for next time. */
                if (insideMesageBody)
                    lineCount -= 1;

                /* Return the line. */
                return line;
            }
        }

        /// <summary>
        /// Wrap a message content object in a NextLineFn wrapper.
        /// </summary>
        /// <param name="msg">Provider's messge content object.</param>
        /// <returns>Wrapped function instance.</returns>
        internal static NextLineFn WrapForRetr(IMessageContent msg)
        {
            /* Setup a flag to be raised when the message content has completed. */
            bool endOfMessage = false;

            /* Return the wrapper function. */
            return Wrap;
            string Wrap()
            {
                /* If we've already seen the end of the message, return null only. */
                if (endOfMessage)
                    return null;

                /* Read the next line from the supplied object. */
                string line = msg.NextLine();

                /* End of message? */
                if (line == null)
                {
                    /* Close the wrapped resource, raise flag and stop. */
                    msg.Close();
                    endOfMessage = true;
                    return null;                    
                }

                /* Otherwise, return loaded line. */
                return line;
            }
        }
    }
}

