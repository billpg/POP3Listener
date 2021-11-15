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
            private readonly Action onClose;
            private readonly Func<bool> onIsSecure;

            public Info(Action onClose, bool isLocalHost, long connectionID, Func<bool> onIsSecure, System.Net.IPAddress clientIP)
            {
                this.onClose = onClose;
                this.IsLocalHost = isLocalHost;
                this.ConnectionID = connectionID;
                this.onIsSecure = onIsSecure;
                this.ClientIP = clientIP;
            }

            internal void Close() => this.onClose();
            internal bool IsLocalHost { get; }
            internal long ConnectionID { get; }
            internal bool IsSecure => this.onIsSecure();
            internal System.Net.IPAddress ClientIP { get; }
        }

        internal static Info Start(TcpClient tcp, bool immediateTls, POP3Listener service, long connectionID)
        {
            /* The initial stream is alway the TCP, but may be replaced with a TLS stream later. */
            Stream str = tcp.GetStream();
            BufferedLineReader lineReader = new BufferedLineReader(str, 1024 * 64);

            /* Setup the information object for the current connetion. */
            Info info = new Info(OnClose, IsLocalHost(), connectionID, IsSecure, GetClientIP());
            CommandHandler handler = new CommandHandler(info, service);

            /* Either hand control to TLS or send a connection banner. */
            if (immediateTls)
                OnTLS();
            else
                OnConnect();

            /* Return an object for tracking this collection to the caller. */
            return info;

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
                /* Construct a TLS object and have it negotiate with the client.
                 * This will call OnEndTLS when it has finished. */
                var tls = new SslStream(str, false);
                tls.BeginAuthenticateAsServer(service.SecureCertificate, OnEndTLS, null);

                /* Called when TLS has completed negotiation with the client. */
                void OnEndTLS(IAsyncResult iar)
                {
                    /* Complete the async operation. */
                    tls.EndAuthenticateAsServer(iar);

                    /* Store this new stream over the top of the TCP one, even if we're about o shit it down. */
                    str = tls;
                    lineReader = new BufferedLineReader(str, 1024 * 64);

                    /* Complain (with TLS) with a critical response if TLS is anything other than TLS 1.2. */
                    if (tls.SslProtocol != System.Security.Authentication.SslProtocols.Tls12)
                        WriteLine("-ERR [SYS/PERM] Only TLS 1.2 is supported by this server.", OnClose);

                    /* Continue as an new conection if we opened with TLS. Otherwise start a new line reader. */
                    if (immediateTls)
                        OnConnect();
                    else
                        OnStartReadLine();
                }
            }

            /* Called when connected, eidther directly or after TLS has negotiated. */
            void OnConnect()
            {
                /* Write the banner. Once it has finished, launch the line reader. */
                WriteLine($"+OK {service.ServiceName}", OnStartReadLine);
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

            /* Called when ready to read a line. */
            void OnStartReadLine()
            {
                /* Start a new stream of reading. */
                lineReader.ReadLine(OnReadCommand);
            }

            /* Called when ready to close the connection down. */
            void OnStartClose()
            {
                /* Close the underlying connection. */
                str.Close();

                /* Inform the service this connection is no longer active. */
                service.RemoveConnection(info);
            }

            /* Called when a line arrives from the client. */
            void OnReadCommand(ByteString linePacket, bool isCompleteLine)
            {
                /* Hanlde stream-closed. */
                if (linePacket == null)
                {
                    OnStartClose();
                }

                /* Handle STLS as a special case. */
                else if (linePacket.AsASCII == "STLS")
                {
                    /* Check we're not already secure. */
                    if (IsSecure())
                    {
                        /* Report error. Nothing to do on complete as StreamLineReader is running. */
                        WriteLine("-ERR Already secure", null);

                        /* Read the next line. */
                        OnStartReadLine();
                    }
                    else
                    {
                        /* Signal to the client to hand over to TLS. */
                        WriteLine("+OK Send TLS ClientHello when ready.", OnTLS);
                    }
                }

                /* Not STLS. */
                else
                {
                    /* Split into parts, with a special case for no-space. */
                    string line = linePacket.AsASCII;
                    string command, pars;
                    int spaceIndex = line.IndexOf(' ');
                    if (spaceIndex < 0)
                    {
                        command = line;
                        pars = null;
                    }
                    else
                    {
                        command = line.Substring(0, spaceIndex);
                        pars = line.Substring(spaceIndex + 1);
                    }

                    /* Handle the command. This function will launch another line read if it wishes. */
                    OnCommand(command.ToUpperInvariant(), pars);
                }
            }

            void OnCommand(string command, string pars)
            {
                /* Run command in a try/catch to pick up response exceptions. */
                PopResponse resp;
                try
                {
                    /* Pass the command over to the session's command handler. */
                    resp = handler.Command(-1, command, pars);
                }
                catch (Exception ex)
                {
                    /* Notify the error. */
                    service.Events.OnError(handler, ex);

                    /* Convert the caught exception to use as the response object. */
                    if (ex is POP3ResponseException rex)
                        resp = rex.AsResponse();

                    /* Turn the default implementation of the provider interface into a non-critical error. */
                    else if (ex is NotImplementedException niex)
                        resp = PopResponse.ERR("Command not available.");

                    /* Pass uncaught exceptions as a critical issue. */
                    else
                        resp = PopResponse.Critical("SYS/TEMP", "System error. Administrators should check logs.");
                }

                /* Send the response to the client. This will trigger the next event. */
                SendResponse(resp);
            }
                
            void SendResponse(PopResponse resp)
            { 
                /* Select the next action based on this being a quit/critical or not. */
                Action onComplete =
                    resp.IsQuit
                    ? (Action)OnStartClose
                    : (Action)OnStartReadLine;

                /* Select the WriteLine action depending on if this is a multi-lne or not. */
                Action onPostWriteLine = 
                    resp.IsMultiLine 
                    ? (Action)OnSendNextResponseLine
                    : (Action)onComplete;


                /* Send initial line. */
                WriteLine(resp.FirstLine, WrapTryCatch(onPostWriteLine, OnCriticalError));

                /* Called when WriteLine finsishes. */
                void OnSendNextResponseLine()
                {
                    /* Get the next line from the response object. */
                    string nextLine = resp.NextLine();

                    /* If there's another line, send it dot-padded. */
                    if (nextLine != null)
                        WriteLine(Helpers.AsDotQuoted(nextLine), WrapTryCatch(OnSendNextResponseLine, OnCriticalError));

                    /* End of multi-line. Send a dot then start reading. */
                    else
                        WriteLine(".", OnStartReadLine);
                }
            }

            void OnCriticalError(Exception ex)
            {
                if (ex is POP3ResponseException popex)
                    SendResponse(popex.AsResponse());
                else
                    SendResponse(PopResponse.Critical("SYS/TEMP", "Critcal error."));
            }

        }

        private static Action WrapTryCatch(Action fn, Action<Exception> onError)
        {
            return Internal;
            void Internal()
            {
                try
                {
                    fn();
                }
                catch (Exception ex)
                {
                    onError(ex);
                }
            }
        }
    }
}
