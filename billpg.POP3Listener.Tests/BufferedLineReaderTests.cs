using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.pop3.Tests
{
    [TestClass]
    public class BufferedLineReaderTests
    {
        private static string[] endsOfLines = new string[] { "\r\n", "\r", "\n" };

        [TestMethod]
        public void BufferedLineReader_Simple()
        {
            /* Open a testing stream and start a line reader. */
            UnitTestNetworkStream.Create(out var readStream, out var writeStream);
            var blr = new BufferedLineReader(readStream, 100);

            /* Set up a general line catcher. */
            using var signal = new AutoResetEvent(false);
            var actualLines = new List<string>();
            void OnLineRead(ByteString line, bool isCompleteLine)
            {
                /* If this is a line... */
                if (line != null)
                {
                    /* Store line. */
                    actualLines.Add(line.AsASCII + (isCompleteLine?"[L]":"[F]"));

                    /* Read another line. */
                    blr.ReadLine(OnLineRead);
                }
                else
                {
                    /* Stream closed. Wake up the primary thread. */
                    signal.Set();
                }
            }

            /* Line generator. */
            var rnd = new Random(84);
            string GenRandomLine()
            {
                /* Select how many digits for the line. */
                int digitCount = rnd.Next(4);
                string addCharsAsString = "";
                while (digitCount-- > 0)
                    addCharsAsString += rnd.Next(4);
                if (addCharsAsString == "")
                    addCharsAsString = "0";

                int addChars = int.Parse(addCharsAsString);
                string line = "";
                while (addChars-- > 0)
                    line += (char)rnd.Next(33, 127);
                return line;
            }

            /* Write some lines to the stream. */
            var expectedLines = new List<string>();
            bool lastLineWasCR = false;
            foreach (int i in Enumerable.Range(0, 1000))
            {
                /* Construct a random length packet. */
                string lineOut = GenRandomLine();
                string endOfLine = endsOfLines[rnd.Next(endsOfLines.Length)];

                /* We want to avoid generating CR, empty line, LF. */
                if (lastLineWasCR && lineOut == "" && endOfLine == "\n")
                    endOfLine = "\r\n";

                /* Store flag if this line ended in a CR. */
                lastLineWasCR = endOfLine == "\r";

                writeStream.WriteString(lineOut + endOfLine);

                /* Read buffer is 100 bytes, so any longer than this will be cut short. */
                while (lineOut.Length >= 100)
                {
                    expectedLines.Add(lineOut.Substring(0, 100) + "[F]");
                    lineOut = lineOut.Substring(100);
                }
                expectedLines.Add($"{lineOut}[L]");

                /* Once a few lines have gone out, start reading. */
                if (i == 10)
                    blr.ReadLine(OnLineRead);               
            }

            /* Close the stream. */
            writeStream.Close();

            /* Wait for the reader to finish. */
            signal.WaitOne();

            /* Check the two collections match. */
            CollectionAssert.AreEqual(expectedLines, actualLines);
        }
    }
}
