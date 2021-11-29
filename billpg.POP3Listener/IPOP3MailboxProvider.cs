/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    public class POP3AuthenticationRequest
    {
        public IPOP3ConnectionInfo ConnectionInfo { get; }
        public string SuppliedUsername { get; }
        public string SuppliedPassword { get; }

        internal POP3AuthenticationRequest(IPOP3ConnectionInfo info, string suppliedUsername, string suppliedPassword)
        {
            this.ConnectionInfo = info;
            this.SuppliedUsername = suppliedUsername;
            this.SuppliedPassword = suppliedPassword;
        }

        public string AuthMailboxID { get; set; } = null;
        public bool MailboxIsReadOnly { get; set; } = false;
    }

    public class POP3MessageRetrievalRequest
    {
        public string AuthMailboxID { get; }
        public string MessageUniqueID { get; }
        public int TopLineCount { get; }
        public bool FullMessage => TopLineCount < 0;
        public Func<string> OnNextLine { get; set; }
        public Action OnClose { get; set; }
        public bool AcceptRetrieval { get; set; }

        internal POP3MessageRetrievalRequest(string authMailboxID, string messageUniqueID, int topLineCount)
        {
            this.AuthMailboxID = authMailboxID;
            this.MessageUniqueID = messageUniqueID;
            this.TopLineCount = topLineCount;
            this.OnNextLine = () => throw new ApplicationException("OnNextLine event handler has not been set.");
            this.OnClose = DefaultOnClose;
            this.AcceptRetrieval = false;
        }
        
        private void DefaultOnClose()
        {
            /* Nothing to do. */
        }

        public void RejectRequest()
        {
            this.AcceptRetrieval = false;
        }

        public void UseEnumerableLines(IEnumerable<string> lines)
        {
            /* Start the enumerable and set an OnNextLine and OnClose to read that enumerator. */
            var lineEnum = lines.GetEnumerator();
            string OnNextLineInternal() => lineEnum.MoveNext() ? lineEnum.Current : null;
            this.OnNextLine = OnNextLineInternal;
            this.OnClose = lineEnum.Dispose;

            /* Flag this as acceptable. */
            this.AcceptRetrieval = true;
        }

        public void UseTextFile(string emlPath, bool deleteAfter)
        {
            /* Open the file for reading. */
            var emlStream = System.IO.File.OpenText(emlPath);

            /* Pass the stream's reader and closer as event handlers. */
            this.OnNextLine = emlStream.ReadLine;
            if (deleteAfter)
                this.OnClose = DisposeAndDelete;
            else
                this.OnClose = emlStream.Dispose;

            /* Function used when deleteAfter==true. */
            void DisposeAndDelete()
            {
                emlStream.Dispose();
                System.IO.File.Delete(emlPath);
            }
        }
    }

    public delegate void RaiseNewMessageEvent(string mailboxID);

    public class POP3ResponseException: Exception
    {
        public string ResponseCode { get; }
        public bool IsCritical { get; }

        public POP3ResponseException(string responseCode, string message, bool isCritical)
            : base(message)
        {
            this.ResponseCode = responseCode;
            this.IsCritical = isCritical;
        }

        public POP3ResponseException(string responseCode, string message) : this(responseCode, message, false) { }

        public POP3ResponseException(string message) : this(null, message, false) { }

        public POP3ResponseException(string message, bool isCritical) : this(null, message, isCritical) { }

        internal PopResponse AsResponse()
        {
            if (IsCritical)
                return PopResponse.Critical(this.ResponseCode, this.Message);
            else
                return PopResponse.ERR(this.ResponseCode, this.Message);
        }
    }


    internal delegate string NextLineFn();

    public interface IPOP3ConnectionInfo
    {
        System.Net.IPAddress ClientIP { get; }
        long ConnectionID { get; }
        string AuthMailboxID { get; }
        string UserNameAtLogin { get; }
        bool IsSecure { get; }
    }

    public interface IMessageContent
    {
        string NextLine();
        void Close();
    }
}

