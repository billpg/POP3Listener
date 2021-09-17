/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace billpg.pop3
{
    public class POP3Listener: IDisposable
    {
        private readonly object mutex;
        private readonly List<TcpListener> listeners;
        private readonly List<SingleConnectionWorker> connections;
        public IIPBanEngine IPBanEngine { get; set; } = new ThreeStrikesBanEngine();
        public IPOP3EventNotification EventNotification { get; set; } = NullEventNotification.Singleton;
        public bool RequireSecureLogin { get; set; }
        public System.Security.Cryptography.X509Certificates.X509Certificate SecureCertificate { get; set; }
        internal readonly System.Threading.ManualResetEvent stopService;
        private static long nextConnectionID = 5000000001;
        internal static long GenConnectionID() => System.Threading.Interlocked.Increment(ref nextConnectionID);

        public POP3Listener()
        {
            mutex = new object();
            stopService = new System.Threading.ManualResetEvent(false);
            listeners = new List<TcpListener>();
            connections = new List<SingleConnectionWorker>();
            RequireSecureLogin = true;
        }

        public string MailboxProviderName { get; set; } = null;

        public delegate void OnAuthenticateDelegate(POP3AuthenticationRequest req);
        public OnAuthenticateDelegate OnAuthenticate { set; get; } = NullAuthenticateRequest;
        private static void NullAuthenticateRequest(POP3AuthenticationRequest req)
        {
            req.AllowRequest = false;
        }

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
            listen.Start();

            /* Store in collection. */
            lock (this.mutex)
                this.listeners.Add(listen);

            /* Set up a one-shot listening event. */
            BeginListen();

            /* Launch a one-shot listen. */
            void BeginListen()
            {
                TryBeginEnd(BeginListenInternal);
                int BeginListenInternal()
                {
                    /* Call the listener object to start listening and to call this call-back.
                     * (We can ignore the IAR returneed here because the call-back will have it. */
                    listen.BeginAcceptTcpClient(OnConnectInternal, null);
                    return 0;
                }
            }

            /* Function called from BeginAcceptTcpClient when a new client is accepted. */
            void OnConnectInternal(IAsyncResult iar) 
            {
                /* Nothing to do if the listener has been stopped. */
                if (listen.Server == null || listen.Server.IsBound == false)
                    return;

                /* Start listening again. This will call BeginAcceptTcpClient again, 
                 * passing in this function as the call-back. */
                BeginListen();

                /* Complete the incoming conection. */
                var tcp = TryBeginEnd(EndListenInternal);
                TcpClient EndListenInternal()
                    => listen.EndAcceptTcpClient(iar);

                /* Only continue if there is an incomming connection. */
                if (tcp != null)
                {
                    /* Start a new connection at the POP3 level. */
                    var pop3 = SingleConnectionWorker.Start(tcp, immediateTls, this);

                    /* Store in the list. */
                    lock (this.mutex)
                        this.connections.Add(pop3);
                }
            }

            /* Call Begin/End ,cathcing and ignoring known exceptions that occurr on shutdown. */
            T TryBeginEnd<T>(Func<T> fn)
            {
                try
                {
                    /* Cal the supplied function and pass along the result. */
                    return fn();
                }

                /* Ignore these two exceptions, known to be thrown during socket shutdown. */
                catch (ObjectDisposedException ex)
                when (ex.ObjectName == "System.Net.Sockets.Socket")
                { }
                catch (InvalidOperationException ex)
                when (ex.Message.Contains("Start()"))
                { }

                /* Return null, indicating the action was unsuccessful. */
                return default;
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
                    con.WaitForStop();
            }
        }

        void IDisposable.Dispose() 
            => Stop();
    }
}

