/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3svc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace billpg.pop3svc.Tests
{
    [TestClass]
    public class LineReaderTests
    {
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        [TestMethod]
        public void LineReader_ReadWeb()
        {
            using (var tcp = new TcpClient())
            {
                tcp.Connect("www.billpg.com", 80);
                var stream = tcp.GetStream();

                byte[] request = UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.billpg.com\r\n\r\n");
                stream.Write(request, 0, request.Length);

                var lr = new LineReader(stream);
                var response = new List<string>();
                while (true)
                {
                    if (lr.TryReadLine(out var line))
                    {
                        string lineAsStrng = UTF8.GetString(line);
                        response.Add(lineAsStrng);

                        if (lineAsStrng.Contains("</html>"))
                            break;
                    }
                    else
                    {
                        bool ready = false;
                        void SetReady()
                        {
                            ready = true;
                        }

                        lr.PopulateRead(SetReady);

                        while (ready == false)
                            Thread.Sleep(100);
                    }
                }

                Assert.AreEqual(14, response.Count);
                int lineIndex = 0;
                Assert.AreEqual("HTTP/1.1 301 Moved Permanently", response[lineIndex++]);
                Assert.IsTrue(response[lineIndex++].StartsWith("Date: "));
                Assert.AreEqual("Server: Apache", response[lineIndex++]);
                Assert.AreEqual("Location: ht"+"tps://www.billpg.com/", response[lineIndex++]);
                Assert.AreEqual("Content-Length: 231", response[lineIndex++]);
                Assert.AreEqual("Content-Type: text/html; charset=iso-8859-1", response[lineIndex++]);
                Assert.AreEqual("", response[lineIndex++]);
                Assert.AreEqual("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">", response[lineIndex++]);
                Assert.AreEqual("<html><head>", response[lineIndex++]);
                Assert.AreEqual("<title>301 Moved Permanently</title>", response[lineIndex++]);
                Assert.AreEqual("</head><body>", response[lineIndex++]);
                Assert.AreEqual("<h1>Moved Permanently</h1>", response[lineIndex++]);
                Assert.AreEqual("<p>The document has moved <a href=\"ht"+"tps://www.billpg.com/\">here</a>.</p>", response[lineIndex++]);
                Assert.AreEqual("</body></html>", response[lineIndex++]);
            }
        }
    }
}

