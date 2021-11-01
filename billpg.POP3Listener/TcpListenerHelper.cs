/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace billpg.pop3
{
    public static class TcpListenerHelper
    {
        /// <summary>
        /// Event handler delegate. Called by StartListen in a new thread when a new connection is made.
        /// </summary>
        /// <param name="tcp"></param>
        public delegate void OnNewConnectionDelegate(TcpClient tcp);

        /// <summary>
        /// Start a TcpListener and have it call the supplied event handler whenever
        /// a new connection arrives. Internal code will handle exceptions known to
        /// occur that should be ignored and will stop listening when the listener's 
        /// Stop function is called.
        /// </summary>
        /// <param name="listen">TcpListener to listen on.</param>
        /// <param name="onNew">Event handler to call inside a 
        /// new thread when a new connection is made.</param>
        public static void StartListen(this TcpListener listen, OnNewConnectionDelegate onNew)
        {
            /* Start the listener. */
            listen.Start();

            /* Set up the event handler for the first incomming connection. */
            BeginListen();

            /* This function sets up the event handler for a single incomming TCP connection.
             * The event handler will call this function again to allow more incomming connections
             * to be handled until someone calls listener.Stop(). */
            void BeginListen()
            {
                /* Call BeginListenInternal inside a try/catch. */
                Helpers.TryCallCatch(BeginListenInternal);
                void BeginListenInternal()
                {
                    /* Call the listener object to start listening and to call OnConnectInternal
                     * once when an incomming connection is made. */
                    listen.BeginAcceptTcpClient(OnConnectInternal, null);
                }
            }

            /* Function called when a new TCP client is accepted. */
            void OnConnectInternal(IAsyncResult iar)
            {
                /* Nothing to do if the listener has been stopped. */
                if (listen.Server == null || listen.Server.IsBound == false)
                    return;

                /* If the listener hasn't been stopped, start listening for a 
                 * single connection again. */
                BeginListen();

                /* Complete the incoming connection by calling End. */
                TcpClient tcp = null;                
                Helpers.TryCallCatch(EndListenInternal);
                void EndListenInternal()
                {
                    tcp = listen.EndAcceptTcpClient(iar);
                }

                /* Only continue if there is an incomming connection. */
                if (tcp != null)
                {
                    /* Pass the newly opened connection back to the caller's event handler. */
                    onNew(tcp);
                }
            }
        }
    }
}
