/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace billpg
{
    /// <summary>
    /// Utility function to create both ends of a TCP connection in NetworkStream form.
    /// </summary>
    public static class UnitTestNetworkStream
    {
        /// <summary>
        /// Create a TCP connection and return both ends.
        /// </summary>
        /// <param name="client">Return the client end of the connection.</param>
        /// <param name="server">Return the servr end of the connection.</param>
        public static void Create(out NetworkStream client, out NetworkStream server)
        {
            /* Create a signal to wait for the connection to be completed. */
            using var connected = new ManualResetEvent(false);

            /* Open a TCP listener and start listening, allowing the OS to pick the port. */
            TcpListener listen = new TcpListener(IPAddress.Loopback, 0);
            listen.Start();
            int port = ((IPEndPoint)listen.LocalEndpoint).Port;

            /* Start listening. Will store the server handle and raise the flag when ready. */
            listen.BeginAcceptTcpClient(OnConnect, null);
            TcpClient tcpServer = null;
            void OnConnect(IAsyncResult iar)
            {
                tcpServer = listen.EndAcceptTcpClient(iar);
                connected.Set();
            }

            /* Open the client end of the connection. */
            var tcpClient = new TcpClient(IPAddress.Loopback.ToString(), port);

            /* Wait for the connection to complete. */
            connected.WaitOne();

            /* Stop listening. */
            listen.Stop();

            /* Return the two ends back to the caller. */
            client = tcpClient.GetStream();
            server = tcpServer.GetStream();            
        }
    }
}
