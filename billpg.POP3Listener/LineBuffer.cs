/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    public class LineBuffer
    {
        private const byte CR = 13;
        private const byte LF = 10;
        private readonly byte[] buffer;
        private int startIndex;
        private int usedLength;
        private bool expectLF;

        public LineBuffer(int bufferSize)
        {
            buffer = new byte[bufferSize];
            startIndex = 0;
            usedLength = 0;
            expectLF = false;
        }

        /* Three parameters ReadAsync will need to populate this buffer. */
        public byte[] Buffer => buffer;
        public int VacantStart => startIndex + usedLength;
        public int VacantLength => buffer.Length - VacantStart;

        public void UpdateUsedBytes(int bytesIn)
        {
            usedLength += bytesIn;
        }

        public ByteString GetLine()
        {
            /* Consume an LF if one is present and we are expecting one. */
            ConsumeLF();

            /* Extract a line if a CR/LF is found. */
            ByteString line = ScanForEndOfLine();

            /* If we just extracted a line... */
            if (line != null)
            {
                /* Consume another LF. (The expect-LF flag may have been raised by ScanForEndOfLine. */
                ConsumeLF();

                /* If the buffer is now empty, set the start index so the next read has most sapce. */
                if (usedLength == 0)
                    startIndex = 0;
            }

            /* Or, did we not find a CR/LF ended line becase the buffer is completely full? */
            else if (startIndex == 0 && usedLength == buffer.Length)
            {
                /* Extract the whole buffer as the line instead. */
                line = ByteString.FromBytes(buffer);
                usedLength = 0;
            }

            /* Or, did we reach the end of the buffer without finding CR/LF but the buffer still has space? */
            else if (VacantLength == 0 && startIndex > 0)
            {
                /* Move the used bytes back to the start so there is space to read ne bytes into.
                 * Line returned will still be NULL as the sign that a Read neds to take place. */
                System.Buffer.BlockCopy(buffer, startIndex, buffer, 0, usedLength);
                startIndex = 0;
            }

            /* Return extracted line (or NULL) to caller. */
            return line;
        }

        private void ConsumeLF()
        {
            /* Consume the LF if we're expecting one. */
            if (usedLength > 0 && expectLF)
            {
                /* Is it an LF? */
                if (buffer[startIndex] == LF)
                {
                    /* Consume it. */
                    startIndex += 1;
                    usedLength -= 1;
                }

                /* No longer expecting an LF either way. */
                expectLF = false;
            }
        }

        private ByteString ScanForEndOfLine()
        { 
            /* Loop through the buffer, looking for a CR/LF. */
            foreach (int offset in Enumerable.Range(0, usedLength))
            {
                /* Is this a CR/LF? */
                byte atOffset = this.buffer[this.startIndex + offset];
                if (atOffset == CR || atOffset == LF)
                {
                    /* Extract the line. */
                    ByteString line = ByteString.FromBytes(this.buffer, startIndex, offset);

                    /* Update the indexes to consume the line and the end-of-line. */
                    this.startIndex += offset + 1;
                    this.usedLength -= offset + 1;

                    /* If the end-of-line was a CR, . */
                    if (atOffset == CR)
                        expectLF = true;

                    /* Return the extacted line. */
                    return line;
                }
            } /* Next byte. */

            /* Reached the end of the buffer with no CR or LF. */
            return null;
        }

        internal void Clear()
        {
            startIndex = 0;
            usedLength = 0;
            expectLF = false;
        }
    }
}
