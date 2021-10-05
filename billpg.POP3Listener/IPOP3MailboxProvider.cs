/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    public delegate void RaiseNewMessageEvent(string userID);

    public interface IPOP3Mailbox
    {
        long MessageSize(IPOP3ConnectionInfo into, string uniqueID);
        IMessageContent MessageContents(IPOP3ConnectionInfo info, string uniqueID);
        bool MailboxIsReadOnly(IPOP3ConnectionInfo info);
        void MessageDelete(IPOP3ConnectionInfo info, IList<string> uniqueIDs);
    }

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

