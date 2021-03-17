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
        internal static NextLineFn WrapForTop(IMessageContent msg, long lineCount)
        {
            /* Update counter, because the blank line doesn't count. */
            lineCount += 1;

            /* Raise a flag if we're inside the message bosy. */
            bool insideMesageBody = false;

            /* Raise a flag once Close has been called. */
            bool calledClose = false;

            return Impl;
            string Impl()
            {
                /* Have we exhausted the requested line count? */
                if (lineCount == 0)
                {
                    /* Inform the message content object we're stopping. */
                    if (calledClose == false)
                    {
                        /* Actually call Close. */
                        msg.Close();

                        /* Raise flag to prevent repeat. */
                        calledClose = true;
                    }

                    /* Signal end-of-stream. */
                    return null;
                }

                /* Load a line. Test for end-of-message. */
                string line = msg.NextLine();
                if (line == null)
                    return null;

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
    }
}

