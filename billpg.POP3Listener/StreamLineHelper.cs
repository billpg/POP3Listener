using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    public class StreamLineHelper
    {
        public Stream Stream { get; set; }
        private readonly byte[] buffer;
        private int startIndex;
        private int usedLength;
        private bool expectLF;
        private int availIndex => startIndex + usedLength;
        private int availLength => buffer.Length - availIndex;
        public bool IsEmpty => usedLength == 0;
        private const byte CR = 13;
        private const byte LF = 10;

        public StreamLineHelper(Stream stream)
        {
            this.Stream = stream;
            this.buffer = new byte[64 * 1024];
            this.startIndex = 0;
            this.usedLength = 0;
        }

        public delegate void OnReadLineDelegate(byte[] line);
        public void ReadLineCall(OnReadLineDelegate onReadLine)
        {
            /* Pass control to internal call-back as if it was called by BeginReadLine. */
            Internal(null);
            void Internal(IAsyncResult iar)
            {
                /* Complete the BeginRead. */
                if (iar != null)
                {
                    /* Update the newly read byte count. The buffer contents will already have been updated. */
                    int newByteCount = this.Stream.EndRead(iar);
                    usedLength += newByteCount;
                }

                /* If there's at least one byte in the buffer and we're expecting an LF... */
                if (expectLF && usedLength > 0)
                { 
                    /* Is it an LF? */
                    if (buffer[startIndex] == LF)
                    {
                        /* Consume the LF silently. */
                        startIndex += 1;
                        usedLength -= 1;
                    }

                    /* Either way, we're not expecting an LF any more. */
                    expectLF = false;
                }                  

                /* Look for a CR or LF. */
                foreach (int offsetIndex in Enumerable.Range(startIndex, usedLength))
                {
                    /* This is one? */
                    byte atOffset = buffer[startIndex + offsetIndex];
                    if (atOffset == CR || atOffset == LF)
                    {
                        /* Copy the bytes out. */
                        byte[] line = new byte[offsetIndex];
                        Buffer.BlockCopy(buffer, startIndex, line, 0, offsetIndex);

                        /* Update the indexes, including the CR or LF. */
                        startIndex += offsetIndex + 1;
                        usedLength -= offsetIndex + 1;

                        /* Is the end-of-line a CR? */
                        if (atOffset == CR)
                        {
                            /* Is there an LF waiting? */
                            if (usedLength > 0 && buffer[startIndex] == LF)
                            {
                                /* Consume the LF too. */
                                startIndex += 1;
                                usedLength -= 1;
                            }
                            /* Otherwise, expect an LF next time. */
                            else
                            {
                                expectLF = true;
                            }
                        }

                        /* Call the call-back. */
                        onReadLine(line);

                        /* Look for aother line. */
                        Internal(null);

                        /* Stop, only triggering a new BeginRead when the buffer is exhausted. */
                        return;
                    }
                }

                /* We didn't find a CR or FL. */

                /* If the buffer is empty, move the counter to the start. */
                if (usedLength == 0)
                    startIndex = 0;

                /* If there is no available space, shift the buffer back to the start. */
                if (availLength == 0 && startIndex > 0)
                {
                    Buffer.BlockCopy(buffer, startIndex, buffer, 0, usedLength);
                    startIndex = 0;
                }

                /* If the buffer is maxed out, return the filled buffer as a single line. */
                if (startIndex == 0 && availLength == 0)
                {
                    /* Copy the buffer and send to caller's callback. */
                    onReadLine(buffer.ToArray());

                    /* Update counters ready for beginRead. */
                    startIndex = 0;
                    usedLength = 0;
                }

                /* No line found, so trigger an async read. */
                this.Stream.BeginRead(buffer, availIndex, availLength, Internal, null);
            }
        }


    }
}
