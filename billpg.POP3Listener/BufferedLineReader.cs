using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace billpg.pop3
{
    public class BufferedLineReader
    {
        public BufferedLineReader( Stream stream, int maxLineLength)
        {
            this.stream = stream;
            this.buffer = new byte[maxLineLength];
            this.startIndex = 0;
            this.usedLength = 0;
            this.expectLF = false;
            this.streamHasClosed = false;
        }

        private const byte CR = 13;
        private const byte LF = 10;
        private readonly Stream stream;
        private readonly byte[] buffer;
        private int startIndex;
        private int usedLength;
        private int availIndex => startIndex + usedLength;
        private int availLength => buffer.Length - availIndex;
        private bool expectLF;
        private bool streamHasClosed;

        public delegate void OnLineReadDelegate(ByteString line, bool isCompleteLine);

        public void ReadLine(OnLineReadDelegate onLineRead)
        {
            /* Pass control to the internal scanner. It will either call 
             * onLineRead or initiate a new BeginRead operation. */
            Scan();

            /* Scan the buffer for a single line. */
            void Scan()
            { 
                /* Are we expecting an LF and we have triffic in the buffer? */
                if (expectLF && usedLength > 0)
                {
                    /* Is the next byte actually an LF? */
                    if (buffer[startIndex] == LF)
                    {
                        startIndex += 1;
                        usedLength -= 1;
                    }

                    /* No longer expecting an LF either way. */
                    expectLF = false;
                }

                /* Scan the buffer for CR/LF. */
                for (int offset=0; offset < usedLength; offset++)
                {
                    /* Check the buffer at this position. */
                    byte atScan = buffer[startIndex + offset];
                    if (atScan == CR || atScan == LF)
                    {
                        /* Found a single line. Extract everything before the CR/LF. */
                        ByteString line = ByteString.FromBytes(buffer, startIndex, offset);

                        /* Consume the byte with the CR/LF. */
                        int lineWithEndLength = offset + 1;
                        startIndex += lineWithEndLength;
                        usedLength -= lineWithEndLength;

                        /* Raise the expect-LF flag if this was a CR. */
                        if (atScan == CR)
                        {
                            expectLF = true;
                        }

                        /* Pass the line to the caller. */
                        onLineRead(line, true);

                        /* Stop. */
                        return;
                    }
                } /* Next offset. */

                /* Reach the end of the buffer scan. Has the stream closed? */
                if (streamHasClosed)
                {
                    /* If there's any traffic in the bufer, send it the caller as the last line. */
                    if (usedLength > 0)
                    {
                        /* Capture last line. */
                        ByteString line = ByteString.FromBytes(buffer, startIndex, usedLength);
                        startIndex = 0;
                        usedLength = 0;

                        /* Send to caller. (Caller may call ReadLine again but streamHasClosed will still be raised.) */
                        onLineRead(line, true);
                    }

                    /* No traffic in he buffer, send a null signla to the caller. */
                    else 
                    {
                        onLineRead(null, true);
                    }

                    /* Don't trigger a new BeginRead either way. */
                    return;
                }

                /* We reached the end without finding a CR or LF. Is the buffer full? */
                if (startIndex == 0 && usedLength == buffer.Length)
                {
                    /* Copy the buffer as single byte-string. */
                    ByteString line = ByteString.FromBytes(buffer);

                    /* Reset the used-length. */
                    startIndex = 0;
                    usedLength = 0;

                    /* Send the captured line to the caller and stop. */
                    onLineRead(line, false);
                    return;
                }

                /* Prepare for a new Read. Move the indexes to the start if the buffer is empty. */
                if (usedLength == 0 && startIndex > 0)
                {
                    startIndex = 0;
                }

                /* If the buffer has reached the end but there is space at the start, move everything back. */
                if (availLength == 0 && startIndex > 0)
                {
                    Buffer.BlockCopy(buffer, startIndex, buffer, 0, usedLength);
                    startIndex = 0;
                }

                /* Invoke BeginRead, which will populate buffer and call OnEndRead. */
                stream.BeginRead(buffer, availIndex, availLength, OnEndRead, null);
            }

            /* Handle read result. */
            void OnEndRead(IAsyncResult iar)
            {
                /* Collect the number of bytes in and update capacity. */
                int bytesIn = 0;
                Helpers.TryCallCatch(OnEndReadInternal);
                void OnEndReadInternal()
                {
                    bytesIn = stream.EndRead(iar);
                }
                usedLength += bytesIn;

                /* If the stream has closed, raise flag for when Scan has finished. */
                if (bytesIn == 0)
                    streamHasClosed = true;

                /* Scan for a line and call OnLineRead if found. */
                Scan();
            }
        }
    }
}
