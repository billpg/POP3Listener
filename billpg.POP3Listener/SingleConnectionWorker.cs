/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace billpg.pop3
{
    internal class SingleConnectionWorker
    {
        private readonly TcpClient tcp;
        private Stream stream;
        private readonly bool immediateTls;
        private readonly POP3Listener service;
        internal readonly CommandHandler handler;
        private readonly Thread worker;
        internal readonly long connectionID;
        internal IPOP3MailboxProvider provider => service.Provider;
        internal readonly AutoResetEvent waitEvent;

        private const byte CR = (byte)'\r';
        private const byte LF = (byte)'\n';
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        private byte[] readBuffer;
        private int readBufferStartIndex;
        private int readBufferUsedCount;
        private bool expectLF;
        private int readBufferFreeIndex => readBufferStartIndex + readBufferUsedCount;
        private int readBufferFreeCount => readBuffer.Length - readBufferFreeIndex;

        public SingleConnectionWorker(TcpClient tcp, bool immediateTls, POP3Listener service)
        {
            this.tcp = tcp;
            this.immediateTls = immediateTls;
            this.service = service;
            this.handler = new CommandHandler(this, service);
            this.worker = new Thread(WorkerMain);
            this.connectionID = POP3Listener.GenConnectionID();
            this.waitEvent = new AutoResetEvent(false);
            this.readBuffer = new byte[10000];
            this.readBufferStartIndex = 0;
            this.readBufferUsedCount = 0;
            this.expectLF = false;
        }

        public System.Net.IPAddress ClientIP
        {
            get
            {
                var endpoint = (System.Net.IPEndPoint)(tcp.Client.RemoteEndPoint);
                return endpoint.Address;
            }
        }

        public bool IsLocalHost
        {
            get
            {
                /* Check the Client is on the same machine. */
                var client = ClientIP.ToString();
                return (client == "127.0.0.1" || client == "::1");
            }
        }

        public bool IsSecure => stream is SslStream;

        internal static SingleConnectionWorker Start(TcpClient tcp, bool immediateTls, POP3Listener service)
        {
            SingleConnectionWorker con = new SingleConnectionWorker(tcp, immediateTls, service);
            con.worker.Start();
            return con;
        }

        private void WorkerMain()
        {
            try
            {
                MainInternal();
            }
            catch
            {
                if (tcp.Connected)
                    WriteLine("-ERR [SYS/TEMP] Critical error. Closing conection.");
            }
            finally
            {
                this.waitEvent.Dispose();
                this.stream.Dispose();
                this.tcp.Close();
            }
        }

        private void MainInternal()
        {
            /* Notify the connection event. */
            service.EventNotification.NewConnection(handler);

            /* Pull out the stream handler. */
            this.stream = tcp.GetStream();

            /* Switch to TLS? */
            if (immediateTls)
                ToTLS();

            /* Get the welcome banner from the provider. */
            SendResponse(handler.Connect());

            /* Keep looping in a read-command/send-response cycle until quit. */
            long nextCommandSequenceID = 1;
            while (true)
            {
                PopResponse resp;
                try
                {
                    /* Wait for a command or a close-down signal. */
                    WaitReadCommand(out string command, out string pars);
                    string commandToUpper = command.ToUpperInvariant();

                    /* If the command is STLS, handle it separately. */
                    if (command == "STLS" && IsSecure == false)
                    {
                        /* Send a positive response to hand control over to TLS. */
                        SendResponse(PopResponse.OKSingle("Begin TLS."));

                        /* Hand control over to TLS. */
                        ToTLS();

                        /* Clear the buffered bytes that could have been piggy-backed prior to TLS. */
                        readBufferStartIndex = 0;
                        readBufferUsedCount = 0;
                        expectLF = false;

                        /* Skip the rest and jump back to waiting for a command. */
                        continue;
                    }

                    /* Pass to command/response object. */
                    resp = handler.Command(nextCommandSequenceID++, commandToUpper, pars);
                }
                catch (Exception ex)
                {
                    /* Notify the error. */
                    service.EventNotification.Error(handler, ex);

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

                /* Send the response (either returned or from exception) as the response. */
                SendResponse(resp);

                /* If this is a quit/critical response, stop now and allow the finally block to close the session. */
                if (resp.IsQuit)
                {
                    service.EventNotification.CloseConnection(handler);
                    return;
                }
            }
        }

        internal void WaitForStop()
        {
            worker.Join();
        }

        private void WaitReadCommand(out string command, out string pars)
        {
            /* Keep looping until we get a line. */
            while (true)
            {
                /* Skip LF if present. */
                if (expectLF && readBufferUsedCount >= 1 && readBuffer[readBufferStartIndex] == LF)
                {
                    readBufferStartIndex += 1;
                    readBufferUsedCount -= 1;
                    expectLF = false;
                }

                /* Look for a CRLF already in the buffer. */
                for (int offset = 0; offset < readBufferUsedCount; offset += 1)
                {
                    /* Byte at current index. */
                    byte atIndex = readBuffer[readBufferStartIndex + offset];

                    /* Is this a CR/LF? */
                    if (atIndex == CR || atIndex == LF)
                    {
                        /* Pull out command. */
                        string line = UTF8.GetString(readBuffer, readBufferStartIndex, offset);
                        service.EventNotification.ConnandReceived(handler, line);

                        /* Move buffer index along, command+terminator byte. */
                        int length = offset + 1;
                        readBufferStartIndex += length;
                        readBufferUsedCount -= length;
                        expectLF = atIndex == CR;

                        /* Split into parts, with a spoecial case for no-space. */
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

                        /* Success. */
                        return;
                    }
                }

                /* At this point, the buffer does not have a single line yet. */

                /* Reset buffer if empty. */
                if (readBufferUsedCount == 0)
                    readBufferStartIndex = 0;

                /* Move used space back to start if free-space exhausted. */
                else if (readBufferFreeCount == 0)
                {
                    /* If no room, end connection. */
                    if (readBufferStartIndex == 0)
                        throw new POP3ResponseException("Command exceeds length limit.", true);

                    /* Move used buffer back to start of buffer. */
                    Buffer.BlockCopy(readBuffer, readBufferStartIndex, readBuffer, 0, readBufferUsedCount);
                    readBufferStartIndex = 0;
                }

                /* Start read operation. */
                var iar = stream.BeginRead(readBuffer, readBufferFreeIndex, readBufferFreeCount, null, null);
                Wait(-1, iar.AsyncWaitHandle);
                int bytesRead = stream.EndRead(iar);

                /* Update used counter. (Start index will stay unchanged.) */
                readBufferUsedCount += bytesRead;
            }
        }

        private void SendResponse(PopResponse resp)
        {
            /* Construct response as a line of text. */
            string prefix = resp.IsOK ? "+OK " : "-ERR ";
            string code = resp.Code == null ? "" : $"[{resp.Code}] ";
            string multilineFlag = resp.IsMultiLine ? " _" : "";

            /* Join all the parts ogther and convert to a byte array. */
            WriteLine(prefix + code + resp.Text + multilineFlag);

            /* Handle multiline responses. */
            if (resp.IsMultiLine)
            {
                /* Keep looping until out of lines. */
                while (true)
                {
                    /* Load a line from the response object. */
                    string line = resp.NextLine();
                    if (line == null)
                    {
                        /* Send a dot and return. */
                        WriteLine(".");
                        return;
                    }

                    /* If the line starts with a ".", add another. */
                    if (line.Length > 1 && line[0] == '.')
                    {
                        line = "." + line;
                    }

                    /* Send line to client. */
                    WriteLine(line);
                }
            }
        }

        private void Wait(int milliseconds, WaitHandle secondHandle)
        {
            /* Wait for the service-stop or the supplied handle. */
            WaitHandle[] waitHandles = new WaitHandle[] { service.stopService, secondHandle };
            int signal = WaitHandle.WaitAny(waitHandles, milliseconds);

            /* If it was the global-stop handle, return an error response. */
            if (signal == 0)
                throw new POP3ResponseException("SYS/PERM", "Service is closing down.", true);
        }

        private void WriteLine(string line)
        {
            byte[] buffer = UTF8.GetBytes(line + "\r\n");
            stream.Write(buffer, 0, buffer.Length);
        }

        private void ToTLS()
        {
            /* Error if already secure. */
            if (IsSecure)
                throw new ApplicationException("Called ToTLS when already secure.");

            /* Load the cert from the listener. */
            var cert = this.service.SecureCertificate;

            /* Open a new stream object that wraps the TCP stream. */
            var tls = new SslStream(stream);
            
            /* Authenticate with the client. */
            tls.AuthenticateAsServer(cert);

            /* Replace the TCP stream with this one. */
            this.stream = tls;

            /* Complain with a critical response if TLS is anything other than v12. */
            if (tls.SslProtocol != System.Security.Authentication.SslProtocols.Tls12)
                throw new POP3ResponseException("Only TLS 1.2 is supported by this server.", true);
        }
    }
}


