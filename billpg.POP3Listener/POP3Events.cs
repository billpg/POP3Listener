/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System.Collections.Generic;
using System.Linq;

namespace billpg.pop3
{
    public class POP3Events
    {
        public delegate void OnAuthenticateDelegate(POP3AuthenticationRequest req);
        public OnAuthenticateDelegate OnAuthenticate { set; get; }

        public delegate IEnumerable<string> OnMessageListDelegate(string mailboxID);
        public OnMessageListDelegate OnMessageList { set; get; }

        public delegate bool OnMessageExistsDelegate(string mailboxID, string messageUniqueID);
        public OnMessageExistsDelegate OnMessageExists { get; set; }

        public delegate long OnMessageSizeDelegate(string mailboxID, string messageUniqueID);
        public OnMessageSizeDelegate OnMessageSize { get; set; }

        public delegate void OnMessageRetrievalDelegate(POP3MessageRetrievalRequest req);
        public OnMessageRetrievalDelegate OnMessageRetrieval { get; set; }

        public delegate void OnMessageDeleteDelegate(string mailboxID, IList<string> uniqueIDs);
        public OnMessageDeleteDelegate OnMessageDelete { get; set; }

        internal POP3Events()
        {
            /* Set up the default event handlers. */
            OnAuthenticate = req => req.AuthMailboxID = null;
            OnMessageList = mailboxID => Enumerable.Empty<string>();
            OnMessageExists = (mailboxID, messageUID) => this.OnMessageList(mailboxID).Contains(messageUID);
            OnMessageSize = (mailboxID, messageUID) => ContentWrappers.MessageSizeByRetrieval(this.OnMessageRetrieval, mailboxID, messageUID);
            OnMessageRetrieval = req => throw new POP3ResponseException("OnMessageRetrieval handler not set.");
            OnMessageDelete = (mailboxID, messageUID) => throw new POP3ResponseException("OnMessageDelete handler not set.");
        }
    }
}