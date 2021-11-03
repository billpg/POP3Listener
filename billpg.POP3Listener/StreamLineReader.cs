/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    public static class StreamLineReader
    {
        public delegate void OnReadLineDelegate(Line line);
        public delegate void OnCloseStreamDelegate();

        [System.Diagnostics.DebuggerDisplay("{AsASCII}")]
        public class Line
        {
            public long Sequence { get; }
            public byte[] Bytes { get; }
            public bool IsCompleteLine { get; }
            public string AsASCII => Encoding.ASCII.GetString(Bytes);
            public string AsUTF8 => Encoding.UTF8.GetString(Bytes);
            public Action StopReader { get; }

            internal Line(byte[] bytes, long sequence, bool isCompleteLine, Action stopReader)
            {
                this.Bytes = bytes;
                this.Sequence = sequence;
                this.IsCompleteLine = isCompleteLine;
                this.StopReader = stopReader;
            }        
        }

        public static void Start(Stream stream, int bufferSize, OnReadLineDelegate onReadLine, OnCloseStreamDelegate onCloseStream)
        {
            /* Mutex protecting threads from each other. */
            object mutex = new object();

            /* The next line sequence value, starting from zero. */
            long nextLineSequence = 0;

            /* The buffer with indexes and derivatives. */
            byte[] buffer = new byte[bufferSize];
            int startIndex = 0;
            int usedLength = 0;
            int availIndex() => startIndex + usedLength;
            int availLength() => buffer.Length - availIndex();

            /* Flag raised if a buffer ends with CR, an LF might be next. */
            bool expectLF = true;

            /* Handy constants. */
            const byte CR = 13;
            const byte LF = 10;

            /* Stop-reader flag with function that can be called by the event handler. */
            bool stopReaderNow = false;
            void StopReaderByCaller() 
                => stopReaderNow = true;

            /* Call BeginRead for the first time. */
            InvokeBeginRead();

            /* Calls BeginRead with the right parameters. */
            void InvokeBeginRead()
            {
                /* Invoke the reader and call the private call-back when ready. */
                Helpers.TryCallCatch(BeginReadInternal);
                void BeginReadInternal()
                    => stream.BeginRead(buffer, availIndex(), availLength(), ReadCallBack, null);
            }

            /* Called when BeginRead finishes from a new thread or from inside BeginRead. */
            void ReadCallBack(IAsyncResult iar)
            {
                /* Wait for other threads to conclude while it works. */
                lock (mutex)
                {
                    /* Complete the read operation. */
                    int newByteCount = 0;
                    Helpers.TryCallCatch(EndReadInternal);
                    void EndReadInternal()
                        => newByteCount = stream.EndRead(iar);

                    /* Closed stream? */
                    if (newByteCount == 0)
                    {
                        /* Any bytes left in the buffer have an implicit end-of-line. */
                        if (usedLength > 0)
                        {
                            byte[] lastLine = ExtractBytes(buffer, startIndex, usedLength);
                            CallOnReadLine(lastLine, true);
                        }

                        /* Call-back to caller than the stream has finished. */
                        onCloseStream();
                        return;
                    }

                    /* Extend the used portion of the buffer by updating for the new bytes added. */
                    usedLength += newByteCount;

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
                    int scanIndex = startIndex;
                    while (scanIndex < availIndex())
                    {
                        /* This is one? */
                        byte atOffset = buffer[scanIndex];
                        if (atOffset == CR || atOffset == LF)
                        {
                            /* Copy the bytes out. */
                            int lineByteCount = scanIndex - startIndex;
                            byte[] line = ExtractBytes(buffer, startIndex, lineByteCount);

                            /* Update the indexes, including the CR or LF. */
                            int lineByteCountIncludingEndOfLine = lineByteCount + 1;
                            startIndex += lineByteCountIncludingEndOfLine;
                            usedLength -= lineByteCountIncludingEndOfLine;

                            /* Is the end-of-line a CR? */
                            if (atOffset == CR)
                            {
                                /* Is there an LF waiting? */
                                if (usedLength > 0 && buffer[startIndex] == LF)
                                {
                                    /* Consume the LF too. */
                                    startIndex += 1;
                                    usedLength -= 1;

                                    /* Move the scan past the LF. */
                                    scanIndex += 1;
                                }

                                /* If the CR was the last byte, expect an LF next buffer. */
                                else if (usedLength == 0)
                                {
                                    expectLF = true;
                                }
                            }

                            /* Call the call-back. */
                            CallOnReadLine(line, true);
                        }

                        /* Move scan index along. */
                        scanIndex++;
                    } /* Next byte. */

                    /* If the buffer is empty, move the counter to the start. */
                    if (usedLength == 0)
                        startIndex = 0;

                    /* If there is no available space, shift the buffer back to the start. */
                    if (availLength() == 0 && startIndex > 0)
                    {
                        /* Only move if there are bytes in the buffer. */
                        if (usedLength > 0)
                            Buffer.BlockCopy(buffer, startIndex, buffer, 0, usedLength);

                        /* Reset the start index nonetheless. */
                        startIndex = 0;
                    }

                    /* If the buffer is maxed out, return the filled buffer as a single line. */
                    if (startIndex == 0 && availLength() == 0)
                    {
                        /* Copy the buffer and send to caller's callback. */
                        CallOnReadLine(buffer.ToArray(), false);

                        /* Update counters ready for beginRead. */
                        startIndex = 0;
                        usedLength = 0;
                    }

                    /* Read the next line, unless the event handler raised the signal not to. */
                    if (stopReaderNow == false)
                        InvokeBeginRead();

                    /* Helper function to call the onReadLine event. */
                    void CallOnReadLine(byte[] line, bool isCompleteLine)
                        => onReadLine(new Line(line, nextLineSequence++, isCompleteLine, StopReaderByCaller));

                } /* Release mutex. */
            }
        }

        private static byte[] ExtractBytes(byte[] from, int startIndex, int length)
        {
            byte[] toBuffer = new byte[length];
            Buffer.BlockCopy(from, startIndex, toBuffer, 0, length);
            return toBuffer;
        }

    }
}
