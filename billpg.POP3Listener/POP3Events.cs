/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;

namespace billpg.pop3
{
    public class POP3Events
    {
        public delegate void OnNewConnectionDelegate(IPOP3ConnectionInfo info);
        public OnNewConnectionDelegate OnNewConnection { get; set; }

        public delegate void OnCommandReceivedDelegate(IPOP3ConnectionInfo info, string command);
        public OnCommandReceivedDelegate OnCommandReceived { get; set; }

        public delegate void OnAuthenticateDelegate(POP3AuthenticationRequest req);
        public OnAuthenticateDelegate OnAuthenticate { set; get; }

        public delegate IEnumerable<string> OnMessageListDelegate(string mailboxID);
        public OnMessageListDelegate OnMessageList { set; get; }

        public delegate long OnMessageSizeDelegate(string mailboxID, string messageUniqueID);
        public OnMessageSizeDelegate OnMessageSize { get; set; }

        public delegate void OnMessageRetrievalDelegate(POP3MessageRetrievalRequest req);
        public OnMessageRetrievalDelegate OnMessageRetrieval { get; set; }

        public delegate void OnMessageDeleteDelegate(string mailboxID, IList<string> uniqueIDs);
        public OnMessageDeleteDelegate OnMessageDelete { get; set; }

        public delegate void OnErrorDelegate(IPOP3ConnectionInfo info, Exception ex);
        public OnErrorDelegate OnError { get; set; }

        internal POP3Events()
        {
            /* Set up the default event handlers. */
            OnNewConnection = info => { };
            OnCommandReceived = (info, command) => { };
            OnAuthenticate = req => req.AuthMailboxID = null;
            OnMessageList = mailboxID => Enumerable.Empty<string>();
            OnMessageSize = (mailboxID, messageUID) => ContentWrappers.MessageSizeByRetrieval(this.OnMessageRetrieval, mailboxID, messageUID);
            OnMessageRetrieval = req => throw new POP3ResponseException("SYS/PERM", "Retrieval not configured.", true);
            OnMessageDelete = (mailboxID, messageUID) => throw new POP3ResponseException("SYS/PERM", "Deletes not configured.", true);
            OnError = (info, ex) => { };
        }
    }
}