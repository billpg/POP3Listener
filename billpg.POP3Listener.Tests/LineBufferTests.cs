/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.pop3.Tests
{
    [TestClass]
    public class LineBufferTests
    {
        private ASCIIEncoding ASCII = new ASCIIEncoding();

        [TestMethod]
        public void LineBuffer_Simple()
        {
            /* Start a new buffer and check the public properties. */
            LineBuffer x = new LineBuffer(10);
            Assert.AreEqual(10, x.Buffer.Length);
            Assert.AreEqual(0, x.VacantStart);
            Assert.AreEqual(10, x.VacantLength);

            /* Function that similates ReadAsync. */
            void SimRead(string text)
            {
                byte[] blob = ASCII.GetBytes(text);
                Assert.IsTrue(blob.Length <= x.VacantLength);
                Buffer.BlockCopy(blob, 0, x.Buffer, x.VacantStart, blob.Length);
                x.UpdateUsedBytes(blob.Length);
            }

            void AssertBufferHasCapacity(int expectedVacant)
            {
                Assert.AreEqual(10-expectedVacant, x.VacantStart);
                Assert.AreEqual(expectedVacant, x.VacantLength);
            }

            void AssertBufferIsEmpty() 
                => AssertBufferHasCapacity(10);
            void AssertBufferIsFull() 
                => AssertBufferHasCapacity(0);
            void AssertRead(string expected) 
                => Assert.AreEqual(expected, x.GetLine().AsASCII);
            
            /* Simulate a read of a complete line which should leave the buffer in an empty state. */
            SimRead("Hello\r\n");
            AssertRead("Hello");
            AssertBufferIsEmpty();

            /* Write two lines which will fill the buffer.
             * The first read will leave the buffer full but the next will empty it. */
            SimRead("Pot\r\nato\r\n");
            AssertBufferIsFull();
            AssertRead("Pot");
            AssertBufferIsFull();
            AssertRead("ato");
            AssertBufferIsEmpty();

            /* Write two lines but the second line goesover the end of the buffer.
             * The second get returns null, indicating a new read is needed which
             * has moved the two "Ru" bytes back to the start. */
            SimRead("Carrot\r\nRu");
            AssertBufferIsFull();
            AssertRead("Carrot");
            AssertBufferIsFull();
            Assert.IsNull(x.GetLine());
            AssertBufferHasCapacity(8);

            /* Complete the interrupted line. */
            SimRead("tabaga\r\n");
            AssertBufferIsFull();
            AssertRead("Rutabaga");
            AssertBufferIsEmpty();

            /* Write a small string with only a CR, then later supply the LF. */
            SimRead("Abcde\r");
            AssertBufferHasCapacity(4);
            AssertRead("Abcde");
            AssertBufferIsEmpty();
            SimRead("\nfghi\n");
            AssertBufferHasCapacity(4);
            AssertRead("fghi");
            AssertBufferIsEmpty();

            /* LFCR are two end-of-lines. */
            SimRead("A\n\rB\n\r");
            AssertRead("A");
            AssertRead("");
            AssertRead("B");
            AssertRead("");
            AssertBufferIsEmpty();

            /* Four CR only lines. */
            SimRead("A\rB\rC\rD\r");
            AssertRead("A");
            AssertRead("B");
            AssertRead("C");
            AssertRead("D");
            AssertBufferIsEmpty();

            /* Four LF only lines. */
            SimRead("E\nF\nG\nH\n");
            AssertRead("E");
            AssertRead("F");
            AssertRead("G");
            AssertRead("H");
            AssertBufferIsEmpty();

            /* Loop each type of end-of-line. (Avoid LFs coming after CRs.) */
            foreach (string endOfLine in new string[] {"\n", "\r", "\r\n"})
            {
                /* Loop each number of blank lines. */
                foreach (int lineCount in Enumerable.Range(0, 10/endOfLine.Length))
                {
                    /* Add the prescribed number of end-of-lines. */
                    SimRead(RepeatString(endOfLine, lineCount));

                    /* Read back as many expected blank lines. */
                    foreach (int readBackCounter in Enumerable.Repeat(0, lineCount))
                        AssertRead("");

                    /* Then read back no-line. */
                    Assert.IsNull(x.GetLine());
                    AssertBufferIsEmpty();
                }
            }

            /* Fill the buffer with two lines, the second will fill the buffer. */
            SimRead("Peas\rAnd\rC");
            AssertRead("Peas");
            AssertRead("And");
            Assert.IsNull(x.GetLine());
            AssertBufferHasCapacity(9);
            SimRead("umberland");
            AssertRead("Cumberland");
            Assert.IsNull(x.GetLine());
            AssertBufferIsEmpty();

            /* Fill the buffer in a single shot. */
            SimRead("Vegetables");
            AssertBufferIsFull();
            AssertRead("Vegetables");
            Assert.IsNull(x.GetLine());
            AssertBufferIsEmpty();
        }

        private string RepeatString(string item, int count)
        {
            StringBuilder s = new StringBuilder();
            foreach (int counter in Enumerable.Range(0, count))
                s.Append(item);
            return s.ToString();
        }
    }
}
