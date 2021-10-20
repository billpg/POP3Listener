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
                if (str.Socket.Connected)
                {

                }
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
    }
}
