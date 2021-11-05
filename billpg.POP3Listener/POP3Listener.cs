/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace billpg.pop3
{
    public class POP3Listener : IDisposable
    {
        private readonly object mutex;
        private readonly List<TcpListener> listeners;
        private readonly List<POP3ServerSession.Info> connections;
        public IIPBanEngine IPBanEngine { get; set; } = new ThreeStrikesBanEngine();
        public bool RequireSecureLogin { get; set; }
        public System.Security.Cryptography.X509Certificates.X509Certificate SecureCertificate { get; set; }
        internal readonly System.Threading.ManualResetEvent stopService;
        private static long nextConnectionID = 5000000001;
        internal static long GenConnectionID() => System.Threading.Interlocked.Increment(ref nextConnectionID);
        public POP3Events Events { get; } = new POP3Events();
        public bool AllowUnknownIDRequests { get; set; } = true;

        public POP3Listener()
        {
            mutex = new object();
            stopService = new System.Threading.ManualResetEvent(false);
            listeners = new List<TcpListener>();
            connections = new List<POP3ServerSession.Info>();
            RequireSecureLogin = true;
        }

        public string ServiceName { get; set; } = "POP3 service by billpg industries https://billpg.com/POP3/";

        public void ListenOnStandard(IPAddress addr)
        {
            ListenOn(addr, 110, false);
            ListenOn(addr, 995, true);
        }

        public void ListenOnHigh(IPAddress addr)
        {
            ListenOn(addr, 1100, false);
            ListenOn(addr, 9955, true);
        }

        public void ListenOn(IPAddress addr, int port, bool immediateTls)
        {
            /* Create new listener. */
            TcpListener listen = new TcpListener(addr, port);
            listen.StartListen(OnNewConnection);

            /* Store in collection. */
            lock (this.mutex)
                this.listeners.Add(listen);

            /* Function called by TcpListener when a new incomming connection is made. */
            void OnNewConnection(TcpClient tcp)
            {
                lock (mutex)
                {
                    var connection = POP3ServerSession.Start(tcp, immediateTls, this);
                    this.connections.Add(connection);
                }
            }
        }

        public void Stop()
        {
            lock (this.mutex)
            {
                /* Raise the flag for other threads to stop. */
                this.stopService.Set();

                /* Stop the listeners. */
                foreach (var listen in this.listeners)
                    listen.Stop();

                /* Wait for the running workers to complete. */
                foreach (var con in this.connections)
                    con.Close();
            }
        }

        void IDisposable.Dispose() 
            => Stop();
    }
}

