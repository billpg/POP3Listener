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
    public class StreamLineReader
    {
        public StreamLineReader(Stream stream, int bufferSize, OnReadLineDelegate onReadLine, OnCloseStreamDelegate onCloseStream)
        {
            this.mutex = new object();
            this.started = false;
            this.Stream = stream;
            this.nextLineSequence = 0;
            this.buffer = new byte[bufferSize];
            this.startIndex = 0;
            this.usedLength = 0;
            this.onReadLine = onReadLine;
            this.onCloseStream = onCloseStream;
        }

        private readonly object mutex;
        private bool started;
        public Stream Stream { get; }
        private long nextLineSequence;
        private readonly byte[] buffer;
        private int startIndex;
        private int usedLength;
        private bool expectLF;
        private int availIndex => startIndex + usedLength;
        private int availLength => buffer.Length - availIndex;
        public bool IsEmpty => usedLength == 0;
        private const byte CR = 13;
        private const byte LF = 10;
        public delegate void OnReadLineDelegate(Line line);
        private OnReadLineDelegate onReadLine;
        public delegate void OnCloseStreamDelegate();
        private OnCloseStreamDelegate onCloseStream;

        [System.Diagnostics.DebuggerDisplay("{AsASCII}")]
        public class Line
        {
            public long Sequence { get; }
            public byte[] Bytes { get;  }
            public bool IsCompleteLine { get; }
            public string AsASCII => Encoding.ASCII.GetString(Bytes);
            public string AsUTF8 => Encoding.UTF8.GetString(Bytes);

            public Line(byte[] bytes, long sequence, bool isCompleteLine)
            {
                this.Bytes = bytes;
                this.Sequence = sequence;
                this.IsCompleteLine = isCompleteLine;
            }        
        }

        public void Start()
        {
            lock (mutex)
            {
                /* Aleady started? */
                if (this.started)
                    throw new ApplicationException("Already started StreamLineReader.");

                /* Call BeginRead for the first time. */
                InvokeBeginRead();

                /* Raise started flag. */
                this.started = true;
            }
        }

        private void InvokeBeginRead()
        {
            /* Invoke the reader and call the private call-back when ready. */
            this.Stream.BeginRead(buffer, availIndex, availLength, ReadCallBack, null);
        }

        private void ReadCallBack(IAsyncResult iar)
        {
            /* Wait for other threads to conclude while it works. */
            lock (mutex)
            {
                /* Complete the read operation. */
                int newByteCount =  this.Stream.EndRead(iar);
                
                /* Closed stream? */
                if (newByteCount == 0)
                {
                    /* Handle any bytes without an end-of-line. */
                    if (usedLength > 0)
                    {
                        byte[] lastLine = ExtractBytes(buffer, startIndex, usedLength);
                        CallOnReadLine(lastLine, true);
                    }

                    this.onCloseStream();
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
                while (scanIndex < availIndex)
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
                if (availLength == 0 && startIndex > 0)
                {
                    /* Only move if there are bytes in the buffer. */
                    if (usedLength > 0)
                        Buffer.BlockCopy(buffer, startIndex, buffer, 0, usedLength);

                    /* Reset the start index nonetheless. */
                    startIndex = 0;
                }

                /* If the buffer is maxed out, return the filled buffer as a single line. */
                if (startIndex == 0 && availLength == 0)
                {
                    /* Copy the buffer and send to caller's callback. */
                    CallOnReadLine(buffer.ToArray(), false);

                    /* Update counters ready for beginRead. */
                    startIndex = 0;
                    usedLength = 0;
                }

                /* Read the next line. */
                InvokeBeginRead();

                /* Helper function to call the onReadLine event. */
                void CallOnReadLine(byte[] line, bool isCompleteLine)
                    => this.onReadLine(new Line(line, nextLineSequence++, isCompleteLine));
                
            } /* Release mutex. */
        }

        private static byte[] ExtractBytes(byte[] from, int startIndex, int length)
        {
            byte[] toBuffer = new byte[length];
            Buffer.BlockCopy(from, startIndex, toBuffer, 0, length);
            return toBuffer;
        }

    }
}
