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

        public delegate IEnumerable<string> OnListMailboxDelegate(string userID);
        public OnListMailboxDelegate OnListMailbox { set; get; }

        public delegate bool OnMessageExistsDelegate(string userID, string messageUniqueID);
        public OnMessageExistsDelegate OnMessageExists { get; set; }

        public delegate long OnMessageSizeDelegate(string userID, string messageUniqueID);
        public OnMessageSizeDelegate OnMessageSize { get; set; }

        public delegate void OnMessageRetrievalDelegate(POP3MessageRetrievalRequest req);
        public OnMessageRetrievalDelegate OnMessageRetrieval { get; set; }

        public delegate void OnMessageDeleteDelegate(string userID, IList<string> uniqueIDs);
        public OnMessageDeleteDelegate OnMessageDelete { get; set; }

        internal POP3Events()
        {
            /* Set up the default event handlers. */
            OnAuthenticate = req => req.AuthUserID = null;
            OnListMailbox = userID => Enumerable.Empty<string>();
            OnMessageExists = (userID, messageUID) => this.OnListMailbox(userID).Contains(messageUID);
            OnMessageSize = (userID, messageUID) => ContentWrappers.MessageSizeByRetrieval(this.OnMessageRetrieval, userID, messageUID);
            OnMessageRetrieval = req => throw new POP3ResponseException("OnMessageRetrieval handler not set.");
            OnMessageDelete = (userID, messageUID) => throw new POP3ResponseException("OnMessageDelete handler not set.");
        }
    }
}