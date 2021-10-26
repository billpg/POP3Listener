using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using billpg.pop3;
using System.Threading;
using System.IO.Pipes;

namespace billpg.pop3.Tests
{
    [TestClass]
    public class TcpListenerHelperTests
    {
        private static readonly Random rnd = new Random();

        [TestMethod]
        public void TcpListenerHelper_OneConnection()
        {
            /* Start counting incomming connections. */
            int listenCounter = 0;

            /* Setup a TcpListener and start it listening using the helper. */
            int port = rnd.Next(1024, 65536);
            TcpListener listen = new TcpListener(IPAddress.Loopback, port);
            listen.StartListen(UnitTestOnAccept);

            /* Function to call when a new connection arrives. */
            void UnitTestOnAccept(TcpClient tcpServer)
            {
                /* Update the counter. */
                listenCounter++;

                /* Send a byte to the client to stop it waiting. */
                using (var strServer = tcpServer.GetStream())
                    strServer.WriteByte((byte)'B');

                /* Close the server connection. */
                tcpServer.Close();
            }

            /* Open a connection as a client. */
            using (var tcpClient = new TcpClient("localhost", port))
            {
                /* Wait for a byte from the server toindicate success. */
                using (var strClient = tcpClient.GetStream())
                    strClient.ReadByte();
            }

            /* Stop listening. */
            listen.Stop();

            /* Check counter. Expecting exactly one incomming connection. */
            Assert.AreEqual(1, listenCounter);
        }

        [TestMethod]
        public void TcpListenerHelper_ManyLongConnections()
        {
            /* test parameters. */
            const int sendBytesCount = 20;
            const int connectionCount = 10;

            /* Start counting. */
            var mutex = new object();
            int counter = 0;
            int activeNowCount = 0;
            int maxActiveNowCount = 0;

            /* Setup a TcpListener and start it listening using the helper. */
            int port = rnd.Next(1024, 65536);
            TcpListener listen = new TcpListener(IPAddress.Loopback, port);
            listen.StartListen(UnitTestOnAccept);

            /* Function to call when a new connection arrives. */
            void UnitTestOnAccept(TcpClient tcpServer)
            {
                /* Update the counter. */
                lock (mutex)
                {
                    counter++;
                    activeNowCount++;
                    maxActiveNowCount = Math.Max(activeNowCount, maxActiveNowCount);
                }

                /* Slowly send five bytes to the client. */
                using (var strServer = tcpServer.GetStream())
                {
                    for (int bytesSent = 0; bytesSent < sendBytesCount; bytesSent++)
                    {
                        Thread.Sleep(1000);                        
                        strServer.WriteByte((byte)bytesSent);
                    }

                    /* Lower the active-now counter just prior to closing the stream. */
                    lock (mutex) activeNowCount--;
                }
            }

            /* Open ten new connections. */
            var clients = 
                Enumerable.Range(0, connectionCount)
                .Select(i => new TcpClient("localhost", port).GetStream())
                .ToList();

            /* Read five bytes from each. */
            for (int byteCount=0; byteCount < sendBytesCount; byteCount++)
            {
                foreach (NetworkStream str in clients)
                {
                    int readByte = str.ReadByte();
                    Assert.AreEqual(byteCount, readByte);
                }
            }

            /* Read the close signal from each. */
            foreach (NetworkStream str in clients)
            {
                int readByte = str.ReadByte();
                Assert.AreEqual(-1, readByte);
            }

            /* Check ten listener threads were launched. */
            lock (mutex)
            {
                Assert.AreEqual(connectionCount, counter);
                Assert.AreEqual(0, activeNowCount);
                Assert.IsTrue(maxActiveNowCount > connectionCount/2);
            }
        }

        [TestMethod]
        public void TestPipeServer_Sync()
        {
            using var server = new System.IO.Pipes.AnonymousPipeServerStream();
            using var client = new AnonymousPipeClientStream(server.GetClientHandleAsString());
            server.WriteString("Rutabaga");
            Assert.AreEqual((byte)'R', client.ReadByte());
            Assert.AreEqual((byte)'u', client.ReadByte());
            Assert.AreEqual((byte)'t', client.ReadByte());
            Assert.AreEqual((byte)'a', client.ReadByte());
            Assert.AreEqual((byte)'b', client.ReadByte());
            Assert.AreEqual((byte)'a', client.ReadByte());
            Assert.AreEqual((byte)'g', client.ReadByte());
            Assert.AreEqual((byte)'a', client.ReadByte());
        }

        [TestMethod]
        public void TestPipeServer_Async()
        {
            using var server = new System.IO.Pipes.AnonymousPipeServerStream();
            using var client = new AnonymousPipeClientStream(server.GetClientHandleAsString());
            using var signal = new ManualResetEvent(false);
            server.WriteString("Rutabaga");

            byte[] buffer = new byte[10];
            int bytesIn = -1;
            client.BeginRead(buffer, 0, 10, TestCallback, null);
            void TestCallback(IAsyncResult iar)
            {
                bytesIn = client.EndRead(iar);
                signal.Set();                
            }

            signal.WaitOne();

            Assert.AreEqual(8, bytesIn);
            Assert.AreEqual("Rutabaga\0\0", Encoding.ASCII.GetString(buffer));

        }

        [TestMethod]
        public void UnitTestNetworkStream_Test()
        {
            UnitTestNetworkStream.Create(out var client, out var server);
            try
            {
                client.WriteLine("Rutabaga");
                Assert.AreEqual("Rutabaga", server.ReadLine());
                server.WriteLine("Carrots");
                Assert.AreEqual("Carrots", client.ReadLine());
            }
            finally
            {
                client.Close();
                server.Close();
            }
        }

        [TestMethod]
        public void TcpLineHelper_ReadLine()
        {
            using var signal = new AutoResetEvent(false);

            /* Start collecting lines. */
            StringBuilder lines = new StringBuilder();
            void AddLine(byte[] line)
            {
                lock (lines)
                {
                    lines.Append(Encoding.ASCII.GetString(line) + "[EOL]");
                    signal.Set();
                }
            }
            string GetLines()
            {
                lock (lines)
                {
                    string value = lines.ToString();
                    lines.Clear();
                    return value;
                }
            }

            /* Open streams to stand in as network streams. */
            using var server = new System.IO.Pipes.AnonymousPipeServerStream();
            using var client = new AnonymousPipeClientStream(server.GetClientHandleAsString());

            /* Set up line reader. Should not (yet) have called back. */
            var helper = new StreamLineHelper(client);
            helper.ReadLineCall(AddLine);
            Assert.AreEqual("", GetLines());

            /* Send some text, but no end-of line. */
            server.WriteString("Rutabaga");
            Assert.AreEqual("", GetLines());

            /* Send just a CR. */
            server.WriteString("\r\n");
            signal.WaitOne();
            Assert.AreEqual("Rutabaga[EOL]", GetLines());
        }
    }
}
