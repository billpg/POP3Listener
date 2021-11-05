/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace billpg.pop3
{
    internal static class POP3ServerSession
    {
        private static readonly ASCIIEncoding ASCII = new ASCIIEncoding();

        internal class Info
        {
            private Action onClose;

            public Info(Action onClose)
            {
                this.onClose = onClose;
            }

            internal void Close() 
                => this.onClose();
        }

        internal static Info Start(TcpClient tcp, bool immediateTls, POP3Listener service)
        {
            /* Initialise data. */
            Stream str = tcp.GetStream();
            SslStream tls = null;

            /* Either hand control to TLS (port 995) or connect normally (port 110). */
            if (immediateTls)
                OnTLS();
            else
                OnConnect();

            /* Return an object for tracking this collection to the caller. */
            return new Info(OnClose);

            /* Called when the caller wants to close this connection down.*/
            void OnClose()
            {
                str.Close();
            }

            System.Net.IPAddress GetClientIP()
            {
                var endpoint = (System.Net.IPEndPoint)(tcp.Client.RemoteEndPoint);
                return endpoint.Address;
            }

            bool IsLocalHost()
            {
                /* Check the Client is on the same machine. */
                var client = GetClientIP().ToString();
                return (client == "127.0.0.1" || client == "::1");
            }

            /* To query if the stream is secure or not. */
            bool IsSecure() => str is SslStream;

            /* Called to initiate TLS, either by connecting or an STLS command. */
            void OnTLS()
            {
                tls = new SslStream(str, false);
                tls.BeginAuthenticateAsServer(service.SecureCertificate, OnEndTLS, null);
            }

            /* Called when TLS has completed negotiation with the client. */
            void OnEndTLS(IAsyncResult iar)
            {
                tls.EndAuthenticateAsServer(iar);

                /* Complain with a critical response if TLS is anything other than v12. */
                if (tls.SslProtocol != System.Security.Authentication.SslProtocols.Tls12)
                    throw new POP3ResponseException("Only TLS 1.2 is supported by this server.", true);

                /* Store this new stream over the top of the TCP one. */
                str = tls;

                /* Continue as an new conection if we opened with TLS. */
                if (immediateTls)
                    OnConnect();
                else
                    OnStartReading();
            }

            /* Called when connected, eidther directly or after TLS has negotiated. */
            void OnConnect()
            {
                /* Write the banner. Once it ha finished, launch the line reader. */
                WriteLine($"+OK {service.ServiceName}", OnStartReading);
            }

            /* Called to send a single line and call the supplied event on completion. */
            void WriteLine(string line, Action onFinishedWrite)
            {
                /* Build line as bytes. */
                byte[] lineAsBytes = ASCII.GetBytes(line + "\r\n");

                /* Send to client. */
                str.BeginWrite(lineAsBytes, 0, lineAsBytes.Length, OnEndWriteLine, null);
                void OnEndWriteLine(IAsyncResult iar)
                {
                    /* End call to BeginWrite. */
                    str.EndWrite(iar);

                    /* Call the next event. */
                    onFinishedWrite?.Invoke();
                }                
            }

            /* Called to start reading. Either after the connection banner or after STLS. */
            void OnStartReading()
            {
                /* Start a new stream of reading. */
                StreamLineReader.Start(str, 1024, OnReadLine, OnCloseStream);
            }

            /* Called when a line arrives from the client. */
            void OnReadLine(StreamLineReader.Line line)
            {
                /* Handle STLS as a special case. */
                if (line.AsASCII == "STLS")
                {
                    /* Check we're not already secure. */
                    if (IsSecure())
                    {
                        /* Report error. Nothing to do on complete as StreamLineReader is running. */
                        WriteLine("-ERR Already secure", null);
                    }
                    else
                    {
                        /* Stop the line reader from reading the TLS ClientHello. */
                        line.StopReader();

                        /* Signal to the client to hand over to TLS. */
                        WriteLine("+OK Send TLS ClientHello when ready.", OnTLS);
                    }
                }

                /* Not STLS. */
                else
                {

                    /* CHANGE THIS */
                    if (line.AsASCII == "CAPA")
                    {
                        if (IsSecure())
                            WriteLine("+OK Capabilities...\\r\nX-TLS True\r\n.", null);
                        else
                            WriteLine("+OK Capabilities...\r\nSTLS\r\nX-TLS False\r\n.", null);
                    }
                    else
                    {
                        WriteLine("-ERR Not Implemented.", null);
                    }


                }
            }

            void OnCloseStream()
            {

            }

        }
    }
}
