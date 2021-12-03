/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace billpg.pop3
{
    internal class POP3ServerSession
    {
        private static readonly ASCIIEncoding ASCII = new ASCIIEncoding();

        private readonly TcpClient tcp;
        private readonly bool immediateTls;
        private readonly POP3Listener service;
        private readonly long connectionID;

        private readonly object mutex;
        private readonly CommandHandler handler;
        private readonly LineBuffer buffer;
        private NetworkStream tcpstr;
        private SslStream tls;
        private Stream currStream => (tls as Stream) ?? (tcpstr as Stream);
        private PopResponse currResp;            

        internal POP3ServerSession(TcpClient tcp, bool immediateTls, POP3Listener service, long connectionID)
        {
            /* Setup private data from params. */
            this.tcp = tcp;
            this.immediateTls = immediateTls;
            this.service = service;
            this.connectionID = connectionID;

            /* Initialise private objects. */
            this.mutex = new object();
            this.handler = new CommandHandler(this, service);
            this.buffer = new LineBuffer(1024 * 64);
            this.tcpstr = null;
            this.tls = null;
            this.currResp = null;
        }

        internal System.Net.IPAddress ClientIP
            => ((System.Net.IPEndPoint)tcp.Client.RemoteEndPoint).Address;

        internal long ConnectionID => connectionID;

        internal bool IsLocalHost
        {
            get
            {
                /* Check the Client is on the same machine. */
                var client = ClientIP.ToString();
                return (client == "127.0.0.1" || client == "::1");
            }
        }

        /* To query if the stream is secure or not. */
        internal bool IsSecure => tls != null;

        internal bool IsActive => tcp.Connected;

        internal void Start()
        {
            /* The initial stream is always the TCP, but may be replaced with a TLS stream later. */
            this.tcpstr = tcp.GetStream();

            /* Either hand control to TLS or send a connection banner. */
            if (immediateTls)
                HandshakeTLS();
            else
                SendConnectBanner();
        }

        internal void CloseConnection()
        {
            lock (mutex)
            {
                tcp.Close();
            }
        }

        private void HandshakeTLS()
        {
            /* Nothing to do if alrady running TLS. */
            if (tls != null)
                return;

            /* Construct a TLS object and have it negotiate with the client.
             * This will call OnEndTLS when it has finished. */
            tls = new SslStream(tcpstr, false);
            tls.AuthenticateAsServerAsync(service.SecureCertificate)
                .LockContinueWith(mutex, OnEndTLS);
        }

        /* Called when TLS has completed negotiation with the client. */
        void OnEndTLS(Task task)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                /* Forget any bytes passed in after the STLS command. */
                buffer.Clear();

                /* Complain (with TLS) with a critical response if TLS is anything other than TLS 1.2. */
                if (tls.SslProtocol != System.Security.Authentication.SslProtocols.Tls12)
                {
                    /* Build response object and send to client. */
                    StartSendResponse(PopResponse.Critical("SYS/PERM", "Only TLS 1.2 is supported by this server."));
                }

                /* Continue as an new conection if we opened with TLS. Otherwise start a new line reader. */
                if (immediateTls)
                    SendConnectBanner();
                else
                    InterpretCommand();
            }
            else
            {

            }
        }


        private void SendConnectBanner()
        {
            StartSendResponse(handler.Connect());
        }

        private void StartSendResponse(PopResponse resp)
        {
            /* Check we're not already in the middle of a response. */
            if (currResp != null)
                throw new ApplicationException("StartSendResponse called before conclusion of response.");
            currResp = resp;

            /* Load the first line of the response. */
            var task = currStream.WriteLineAsync(currResp.FirstLine);

            /* Select what to do after this line, depending on the response type. */
            if (resp.IsMultiLine)
                task.LockContinueWith(mutex, ContinueSendResponse);
            else if (resp.IsQuit)
                task.LockContinueWith(mutex, CompleteSendResponseQuit);
            else
                task.LockContinueWith(mutex, CompleteSendResponseRead);
        }

        private void ContinueSendResponse(Task task)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                /* Next line? */
                string nextLine = currResp.NextLine();
                if (nextLine != null)
                {
                    string quotedNextLine = Helpers.AsDotQuoted(nextLine);
                    currStream.WriteLineAsync(quotedNextLine)
                        .LockContinueWith(mutex, ContinueSendResponse);
                }

                /* No more lines in multi-line response. */
                else
                {
                    /* Send a dot multi-line teminator and return to reading command. */
                    currStream.WriteLineAsync(".")
                        .LockContinueWith(mutex, CompleteSendResponseRead);
                }
            }
            else
            {

            }
        }

        private void CompleteSendResponseQuit(Task task)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                currResp = null;
                CloseConnection();
            }
            else
            {

            }
        }

        private void CompleteSendResponseRead(Task task)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                currResp = null;
                InterpretCommand();
            }
            else
            {

            }
        }

        private void CompleteSendResponseHandshakeTLS(Task task)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                currResp = null;
                HandshakeTLS();
            }
            else
            {

            }
        }

        private void InterpretCommand()
        {
            /* Check if there is already a command in the buffer. */
            ByteString linePacket = buffer.GetLine();

            /* If there isn't a line of text in the buffer, we need to populate it. */
            if (linePacket == null)
            {
                /* First check if the channel has closed, if so there's nothing to do. */
                if (tcp.Connected == false)
                    return;

                /* Still good. */
                currStream.ReadAsync(buffer.Buffer, buffer.VacantStart, buffer.VacantLength)
                    .LockContinueWith(mutex, CompleteRead);
            }

            /* Handle STLS as a special case. */
            else if (linePacket.AsASCII == "STLS")
            {
                /* Check we're not already secure. */
                if (IsSecure)
                {
                    /* Report error. Nothing to do on complete as StreamLineReader is running. */
                    StartSendResponse(PopResponse.ERR("Already secure."));
                }
                else
                {
                    /* Signal to the client to hand over to TLS. */
                    currStream.WriteLineAsync("+OK Send TLS ClientHello when ready.")
                        .LockContinueWith(mutex, CompleteSendResponseHandshakeTLS);
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

        void CompleteRead(Task<int> task)
        {
            /* Did the read fail because the underlying connection was closed? */
            if (task.IsConnectionClosed())
            {
                CloseConnection();
            }

            /* If read was successful, start command interpreter. */
            else if (task.Status == TaskStatus.RanToCompletion)
            {
                buffer.UpdateUsedBytes(task.Result);
                InterpretCommand();
            } 
            else if (task.Status == TaskStatus.Faulted)
            {

            }
            else
            {

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
            StartSendResponse(resp);
        }


    }
}
#if false


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
            NetworkStream netStr =  tcp.GetStream();
            SslStream tls = null;
            Stream str = netStr;

            /* Set up a rotataing buffer to store incoming lines. */
            LineBuffer buffer = new LineBuffer(1024 * 64);

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




            /* Called to initiate TLS, either by connecting or an STLS command. */
            void OnTLS()
            {
                /* Construct a TLS object and have it negotiate with the client.
                 * This will call OnEndTLS when it has finished. */
                tls = new SslStream(str, false);
                tls.AuthenticateAsServerAsync(service.SecureCertificate)
                    .ContinueWith(OnEndTLS);

                /* Called when TLS has completed negotiation with the client. */
                void OnEndTLS(Task task)
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        /* Store this new stream over the top of the TCP one, even if we're about to shut it down. */
                        str = tls;

                        /* Forget any bytes passed in after the STLS command. */
                        buffer.Clear();

                        /* Complain (with TLS) with a critical response if TLS is anything other than TLS 1.2. */
                        if (tls.SslProtocol != System.Security.Authentication.SslProtocols.Tls12)
                            WriteLine("-ERR [SYS/PERM] Only TLS 1.2 is supported by this server.", OnClose);

                        /* Continue as an new conection if we opened with TLS. Otherwise start a new line reader. */
                        if (immediateTls)
                            OnConnect();
                        else
                            OnStartReadLine();
                    }
                    else
                    {

                    }
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
                str.WriteAsync(lineAsBytes, 0, lineAsBytes.Length)
                    .ContinueWith(OnEndWriteLine);
                void OnEndWriteLine(Task task)
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        /* Call the next event. */
                        onFinishedWrite?.Invoke();
                    }
                    else
                    {

                    }
                }                
            }

            /* Called when ready to read a line. */
            void OnStartReadLine()
            {
                /* Start a new stream of reading. */
                str.ReadAsync(buffer.Buffer, buffer.VacantStart, buffer.VacantLength)
                    .ContinueWith()
            }

            /* Called when ready to close the connection down. */
            void OnStartClose()
            {
                /* Close the underlying connection. */
                str.Close();

                /* Inform the service this connection is no longer active. */
                service.RemoveConnection(info);
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
#endif