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

            /* Setup a TcpListener and start it listening using he helper. */
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
    }
}
