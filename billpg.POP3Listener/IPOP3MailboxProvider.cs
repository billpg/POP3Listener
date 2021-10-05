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

        public string AuthUserID { get; set; } = null;
        public bool MailboxIsReadOnly { get; set; } = false;
    }

    public class POP3MessageRetrievalRequest
    {
        public string AuthUserID { get; }
        public string MessageUniqueID { get; }
        public int TopLineCount { get; }
        public bool FullMessage => TopLineCount < 0;
        public Func<string> OnNextLine { get; set; }
        public Action OnClose { get; set; }
        public bool AcceptRetrieval { get; set; }

        internal POP3MessageRetrievalRequest(string authUserID, string messageUniqueID, int topLineCount)
        {
            this.AuthUserID = authUserID;
            this.MessageUniqueID = messageUniqueID;
            this.TopLineCount = topLineCount;
            this.OnNextLine = () => throw new POP3ResponseException("OnNextLine event handler has not been set.");
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

        public void UseLines(IEnumerable<string> lines)
        {
            /* Start the enumerable and set an OnNextLine and OnClose to read that enumerator. */
            var lineEnum = lines.GetEnumerator();
            string OnNextLineInternal() => lineEnum.MoveNext() ? lineEnum.Current : null;
            this.OnNextLine = OnNextLineInternal;
            this.OnClose = lineEnum.Dispose;

            /* Flag this as acceptable. */
            this.AcceptRetrieval = true;
        }

        public void UseTextFile(string path)
            => UseLines(System.IO.File.ReadLines(path));
    }

    public delegate void RaiseNewMessageEvent(string userID);

    public interface IPOP3EventNotification
    {
        void NewConnection(IPOP3ConnectionInfo info);
        void ConnandReceived(IPOP3ConnectionInfo info, string command);
        void Error(IPOP3ConnectionInfo info, Exception ex);
        void CloseConnection(IPOP3ConnectionInfo info);
    }

    internal class NullEventNotification : IPOP3EventNotification
    {
        private NullEventNotification() { }
        internal static NullEventNotification Singleton = new NullEventNotification();

        void IPOP3EventNotification.CloseConnection(IPOP3ConnectionInfo info) { }
        void IPOP3EventNotification.ConnandReceived(IPOP3ConnectionInfo info, string command) { }
        void IPOP3EventNotification.Error(IPOP3ConnectionInfo info, Exception ex) { }
        void IPOP3EventNotification.NewConnection(IPOP3ConnectionInfo info) { }
    }

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


    public delegate string NextLineFn();

    public interface IPOP3ConnectionInfo
    {
        System.Net.IPAddress ClientIP { get; }
        long ConnectionID { get; }
        string AuthUserID { get; }
        string UserNameAtLogin { get; }
        bool IsSecure { get; }
        object ProviderTag { get; set; }
    }

    public interface IMessageContent
    {
        string NextLine();
        void Close();
    }
}

