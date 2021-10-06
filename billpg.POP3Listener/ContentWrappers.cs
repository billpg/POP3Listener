/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Text;

namespace billpg.pop3
{
    internal static class ContentWrappers
    {
        /// <summary>
        /// Handy UTF8 NO-BOM constructed once.
        /// </summary>
        private static UTF8Encoding UTF8_NO_BOM = new UTF8Encoding(false);

        /// <summary>
        /// Helper function that finds a message's size by retrieving the whole message and counting bytes.
        /// </summary>
        /// <param name="onRetrieveHandler">Currenrt message retrieval handler to invoke.</param>
        /// <param name="mailboxID">User's ID.</param>
        /// <param name="messageUniqueID">Message unique ID.</param>
        /// <returns>Total message byte count.</returns>
        internal static long MessageSizeByRetrieval(POP3Events.OnMessageRetrievalDelegate onRetrieveHandler, string mailboxID, string messageUniqueID)
        {
            /* Construct a full-message retrival request and pass to current handler for it to confiure. */
            POP3MessageRetrievalRequest request = new POP3MessageRetrievalRequest(mailboxID, messageUniqueID, -1);
            onRetrieveHandler(request);

            /* Loop through lines, counting bytes as we go. */
            long byteCountSoFar = 0;
            while (true)
            {
                /* Load line. If end-of-message, return completed byte count. */
                string line = request.OnNextLine();
                if (line == null)
                    return byteCountSoFar;

                /* Update the count, adding two for CRLF. */
                byteCountSoFar += UTF8_NO_BOM.GetByteCount(line) + 2;
            }
        }

        /// <summary>
        /// Wrap a message content interface with a NextLineFn function suiatbe for a TOP command.
        /// </summary>
        /// <param name="msg">Provider's message content object.</param>
        /// <param name="lineCount">Numbe rof lines after the ehader.</param>
        /// <returns>Wrapper function.</returns>
        internal static NextLineFn WrapForTop(POP3MessageRetrievalRequest request, long lineCount)
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
                request.OnClose();

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
                string line = request.OnNextLine();
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
        internal static NextLineFn WrapForRetr(POP3MessageRetrievalRequest request)
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
                string line = request.OnNextLine();

                /* End of message? */
                if (line == null)
                {
                    /* Close the wrapped resource, raise flag and stop. */
                    request.OnClose();
                    endOfMessage = true;
                    return null;                    
                }

                /* Otherwise, return loaded line. */
                return line;
            }
        }
    }
}

