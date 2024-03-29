/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.pop3
{
    internal class CommandHandler: IPOP3ConnectionInfo
    {
        private readonly POP3Listener service;

        private string unauthUserName = null;
        private string userNameAtLogin = null;
        private string authMailboxID = null;
        private bool mailboxIsReadOnly = true;
        private bool authenticated => authMailboxID != null;
        private IList<string> uniqueIDs = null;
        private readonly List<string> deletedUniqueIDs = new List<string>();
        private readonly POP3ServerSession activeConnection;
        private bool isSleeping = false;

        System.Net.IPAddress IPOP3ConnectionInfo.ClientIP => activeConnection.ClientIP;

        long IPOP3ConnectionInfo.ConnectionID => activeConnection.ConnectionID;

        string IPOP3ConnectionInfo.AuthMailboxID => this.authMailboxID;

        string IPOP3ConnectionInfo.UserNameAtLogin => authenticated ? userNameAtLogin : null;

        bool IPOP3ConnectionInfo.IsSecure => activeConnection.IsSecure;

        private bool IsUserLoginAllowed => (service.RequireSecureLogin == false || activeConnection.IsSecure || activeConnection.IsLocalHost);

        private static readonly IList<string> capabilities = new List<string>
        {
            /* Standard CAPA Tags. (Not including STLS/USER as will be added only if applicable.) */
            "TOP", "RESP-CODES", "PIPELINING", "UIDL", "AUTH-RESP-CODE",
            /* Mine. */
            "UID-PARAM", "MULTI-LINE-IND", "DELI", "SLEE-WAKE", "QAUT"
        }.AsReadOnly();

        private static readonly IList<string> allowedUnauth = new List<string>
        {
            "NOOP", "CAPA", "USER", "PASS", "XLOG", "STLS", "QUIT"
        }.AsReadOnly();

        private static readonly IList<string> allowedSleeping = new List<string>
        {
            "NOOP", "WAKE", "QUIT"
        }.AsReadOnly();

        private static PopResponse BadCommandSyntaxResponse => PopResponse.ERR("Bad command syntax.");

        private const string UidParamPrefix = "UID:";

        public CommandHandler(POP3ServerSession activeConnection, POP3Listener service)
        {
            this.activeConnection = activeConnection;
            this.service = service;
        }

        internal PopResponse Connect()
            => PopResponse.OKSingle(service.ServiceName);

        internal PopResponse Command(long commandSequenceID, string command, string pars)
        {
            /* Only these commands are allowed in un-authenticated state. */
            if (this.authenticated == false && allowedUnauth.Contains(command) == false)
                return PopResponse.ERR("Authenticate first.");

            /* Check commands not allowed in sleeping state. */
            if (this.isSleeping && allowedSleeping.Contains(command) == false)
                return PopResponse.ERR("This command is not allowed in a sleeping state. (Only NOOP, QUIT or WAKE.)");

            /* Switch for command. */
            switch (command)
            {
                case "NOOP": return NOOP();
                case "CAPA": return CAPA();
                case "USER": return USER(pars);
                case "PASS": return PASS(pars);
                case "XLOG": return XLOG(pars);
                case "STAT": return STAT();
                case "LIST": return LIST(pars);
                case "UIDL": return UIDL(pars);
                case "RETR": return RETR(pars);
                case "TOP":  return TOP(pars);
                case "DELE": return DELE(pars);
                case "DELI": return DELI(pars);
                case "QUIT": return QUIT();
                case "QAUT": return QAUT();
                case "RSET": return RSET();
                case "SLEE": return SLEE();
                case "WAKE": return WAKE();
                default:
                    return PopResponse.ERR("Unknown command: " + command);
            }
        }

        private PopResponse CAPA()
        {
            /* Start with standard list. */
            List<string> resp = new List<string>(capabilities);

            /* Add IMPLEMENTATION from provider. */
            resp.Insert(0, "IMPLEMENTATION " + service.ServiceName);

            /* Add link to my site if ServiceName doesn't include it. */
            if (service.ServiceName.Contains("/billpg.com/") == false)
                resp.Add("X-ENGINE http://billpg.com/POP3/");

            /* Add STLS only if we have a TLS cert and not already secure. */
            if (activeConnection.IsSecure == false && service.SecureCertificate != null)
                resp.Add("STLS");

            /* Add USER if either insecure logins are allowed or we're already secure. */
            if (IsUserLoginAllowed)
                resp.Add("USER");

            /* Add header indicating if connection is secure. */
            resp.Add($"X-TLS {activeConnection.IsSecure}");

            /* Sort list in order. */
            resp.Sort();

            /* Return completed list to client. */
            return PopResponse.OKMulti("Capabilities are...", resp);
        }

        private PopResponse USER(string claimedUser)
        {
            /* Reject if we need to secure first. */
            if (this.IsUserLoginAllowed == false)
                return PopResponse.ERR("Call STLS and negotiate TLS first.");

            /* Only applicable if no user yet. */
            if (this.authenticated || this.unauthUserName != null)
                return PopResponse.ERR("Already called USER.");

            /* Claimed name must have at least one character. */
            if (claimedUser.Length == 0)
                return PopResponse.ERR("Invalid user name.");

            /* Store the user name in readiness for the PASS command. */
            this.unauthUserName = claimedUser;

            /* Allow client to provide password. */
            return PopResponse.OKSingle("Thank you. Send password.");
        }

        private PopResponse PASS(string claimedPassClear)
        {
            /* Reusable bad password response. */
            PopResponse badPasswordResponse() 
                => PopResponse.ERR("AUTH", "Wrong username or password.");

            /* Check if this IP has been banned. */
            System.Net.IPAddress clientIP = ((IPOP3ConnectionInfo)this).ClientIP;
            if (this.service.IPBanEngine.IsBanned(clientIP))
            {
                /* Renew the ban. */
                this.service.IPBanEngine.RegisterFailedAttempt(clientIP);

                /* Return the same response for a bad password. */                
                return badPasswordResponse();
            }

            /* Only valid if username provided and not yet authenticated. */
            if (this.unauthUserName == null)
                return PopResponse.ERR("Call USER before PASS.");
            if (this.authenticated)
                return PopResponse.ERR("Already authenticated.");

            /* Construct an authentication request object and pass to provider. */
            POP3AuthenticationRequest authreq = new POP3AuthenticationRequest(this, this.unauthUserName, claimedPassClear);
            service.Events.OnAuthenticate(authreq);

            /* Accepted? */
            if (authreq.AuthMailboxID != null)
            {
                /* Collect the authenticated user ID. */
                this.userNameAtLogin = unauthUserName;
                this.unauthUserName = null;
                this.authMailboxID = authreq.AuthMailboxID;
                this.mailboxIsReadOnly = authreq.MailboxIsReadOnly;

                /* Fix the collecton of messages IDs for this session. */
                this.uniqueIDs = RefreshUniqueIDsFromMailbox();

                /* Set new state and return in the affimative */
                return PopResponse.OKSingle("Welcome.");
            }

            /* Denied. */
            else
            {
                /* Reset attempted user. */
                this.unauthUserName = null;

                /* Raise the failed attempt count. */
                this.service.IPBanEngine.RegisterFailedAttempt(clientIP);

                /* Send response with the "AUTH" flag, indicating the error is due to credentials and not a random fault. */
                return badPasswordResponse();
            }
        }

        private IList<string> RefreshUniqueIDsFromMailbox()
        {
            /* Load the new list of IDs from the mailbox interface object. */
            var newUniqueIDs = this.service.Events.OnMessageList(this.authMailboxID).ToList().AsReadOnly();

            /* If any are outside 33-126, return with a critical error. Otherwise, return them. */
            if (newUniqueIDs.All(IsGoodUniqueID))
                return newUniqueIDs;
            else
                throw new POP3ResponseException("Unique ID strings must be printable ASCII only.", true);

            /* Helper function that checks uniqueIDs for validity. */
            bool IsGoodUniqueID(string uid)
            {
                /* Shortcut empty strings. */
                if (string.IsNullOrEmpty(uid))
                    return false;

                /* Long strings are bad. */
                if (uid.Length > 70)
                    return false;

                /* A single _ might be seen as a long response continuation. */
                if (uid == "_")
                    return false;

                /* All characters must be '!' to '~'. */
                if (uid.Any(ch => (int)ch < (int)'!' || (int)ch > (int)'~'))
                    return false;

                /* Otherwise its fine. */
                return true;
            }
        }

        private PopResponse XLOG(string pars)
        {
            /* Split parameters into user and password. */
            int spaceIndex = pars.IndexOf(' ');
            if (spaceIndex < 1)
                return PopResponse.ERR("Syntax: XLOG (username) (password)");

            /* Extract the two parts. */
            this.unauthUserName = pars.Substring(0, spaceIndex);
            string claimedPassword = pars.Substring(spaceIndex + 1);

            /* Call through to the PASS command handler. */
            return PASS(claimedPassword);
        }

        private PopResponse STAT()
        {
            /* Collect the not-flagged messages. */
            var countedUniqueIDs = this.uniqueIDs.Except(this.deletedUniqueIDs).ToList();

            /* Loop through, loading the message size for each one. */
            long totalBytes = countedUniqueIDs.Select(uniqueID => this.service.Events.OnMessageSize(this.authMailboxID, uniqueID)).Sum();

            /* Return response. */
            return PopResponse.OKSingle($"{countedUniqueIDs.Count} {totalBytes}");
        }

        private PopResponse LIST(string id)
        {
            return PerMessageOrSingle(id, Translate, "Message sizes follow...");
            string Translate(int messageID, string uniqueID)
                => $"{messageID} {service.Events.OnMessageSize(this.authMailboxID, uniqueID)}";
        }

        private PopResponse UIDL(string id)
        {
            return PerMessageOrSingle(id, Translate, "Unique-IDs follow...");
            string Translate(int messageID, string uniqueID)
                => $"{messageID} {uniqueID}";
        }

        private PopResponse PerMessageOrSingle(string id, MultiLinePerMessageTranslate translateFn, string firstLineText)
        {
            /* If no parameters, return as list. */
            if (string.IsNullOrEmpty(id))
                return MultiLinePerMessage(firstLineText, translateFn);

            /* Parse for message-id or unique-id. */
            ParseForUniqueId(id, out int messageID, out string uniqueID);

            /* Return one-line response. */
            return PopResponse.OKSingle(translateFn(messageID, uniqueID));
        }

        private delegate string MultiLinePerMessageTranslate(int messageID, string uniqueID);
        private PopResponse MultiLinePerMessage(string introText, MultiLinePerMessageTranslate translate)
        {
            /* Function to return information about the next message. */
            int messageID = 0;
            string NextResponseLine()
            {
                /* Keep looping to skip over flagged-as-deleted messages. */
                while (true)
                {
                    /* Move to next message, starting from one. */
                    messageID++;

                    /* If exceeded the uniqueID count, stop. */
                    if (messageID > this.uniqueIDs.Count)
                        return null;

                    /* Load uniqueID for this message ID. */
                    string uniqueID = this.uniqueIDs[messageID - 1];

                    /* If this has been deleted, skip over it. */
                    if (deletedUniqueIDs.Contains(uniqueID))
                        continue;

                    /* Call back to thetranslator function with the message ID and unique ID. */
                    return translate(messageID, uniqueID);
                }
            }

            /* Return as a multi-line response. */
            return PopResponse.OKMulti(introText, NextResponseLine);
        }

        private PopResponse RETR(string pars) 
            => RetrOrTop(pars, -1);

        private PopResponse TOP(string pars)
        {
            /* If no parameters, error. */
            if (string.IsNullOrEmpty(pars))
                return BadCommandSyntaxResponse;

            /* Split off the line-count paramter. */
            int spaceIndex = pars.LastIndexOf(' ');
            if (spaceIndex < 0)
                return BadCommandSyntaxResponse;

            /* Parse the line count after the last space. */
            if (int.TryParse(pars.Substring(spaceIndex).Trim(), out int lineCount) == false)
                return BadCommandSyntaxResponse;

            /* Continue as with RETR internals with the line count has been cut off. */
            return RetrOrTop(pars.Substring(0, spaceIndex).Trim(), lineCount);
        }

        private PopResponse RetrOrTop(string pars, int lineCountSend)
        {
            /* If no parameters, error. */
            if (string.IsNullOrEmpty(pars))
                return BadCommandSyntaxResponse;

            /* Parse for message-id or unique-id. */
            ParseForUniqueId(pars, out _, out string uniqueID);

            /* Load the message from the provider. */
            POP3MessageRetrievalRequest request = new POP3MessageRetrievalRequest(authMailboxID, uniqueID, lineCountSend);
            this.service.Events.OnMessageRetrieval(request);

            /* If RETR, return the whole message without a wrapper. */
            if (lineCountSend < 0)
                return PopResponse.OKMulti("Message text follows...", ContentWrappers.WrapForRetr(request));

            /* If TOP (include header but stop early. */
            else
                return PopResponse.OKMulti($"Header and first {lineCountSend} lines...", ContentWrappers.WrapForTop(request, lineCountSend));

            /* Invalid combination. */
            throw new ApplicationException("Called RetrTopResu with a bad combination of line counts.");
        }
             
        private PopResponse DELE(string pars)
        {
            return DeleteWrapper(pars, Internal);
            PopResponse Internal(string uniqueID)
            {
                /* Add the unique-id to the list of flags and return success. */
                deletedUniqueIDs.Add(uniqueID);
                return PopResponse.OKSingle($"Message UID:{uniqueID} flagged for delete on QUIT or SLEE.");
            }
        }

        private PopResponse DELI(string pars)
        {
            return DeleteWrapper(pars, Internal);
            PopResponse Internal(string uniqueID)
            {
                /* Delete this single message with the mailbox provider. */
                this.service.Events.OnMessageDelete(this.authMailboxID, new List<string> { uniqueID }.AsReadOnly());

                /* Store this id so it will be excluded from future LIST/UIDL/tec. */
                this.deletedUniqueIDs.Add(uniqueID);

                /* Return success. */
                return PopResponse.OKSingle($"Deleted message. UID:{uniqueID}");
            }
        }

        private PopResponse DeleteWrapper(string pars, Func<string, PopResponse> action)
        {
            /* If no parameters, error. */
            if (string.IsNullOrEmpty(pars))
                return BadCommandSyntaxResponse;

            /* If the mailbox is read-only, return error. */
            if (this.mailboxIsReadOnly)
                return PopResponse.ERR("READ-ONLY", "This mailbox is read-only.");

            /* Parse for unique ID. Will also check if message is already flagged. */
            ParseForUniqueId(pars, out _, out string uniqueID);

            /* Pass control back to caller. */
            return action(uniqueID);
        }

        private PopResponse QUIT()
        {
            /* Handle where the client has not logged in. */
            if (this.authenticated == false)
                return PopResponse.Quit("Closing connection without authenticating.");

            /* Delete the flagged messages and reset state. */
            DeleteFlaggedMessages(out int messageCount);

            /* Work ouut how to report the message count. */
            string messagesDeletedReport = "";
            if (messageCount > 0)
                messagesDeletedReport = $"{messageCount} messages deleted. ";

            /* Report success. */
            return PopResponse.Quit(messagesDeletedReport + "Closing connection.");
        }

        private void DeleteFlaggedMessages(out int messageCount)
        {
            /* Store the message count before attempting. Exit early if no messages to delete. */
            messageCount = deletedUniqueIDs.Count;
            if (messageCount == 0)
                return;

            /* Send all the flagged unique IDs to the provider. */
            this.service.Events.OnMessageDelete(this.authMailboxID, deletedUniqueIDs.AsReadOnly());

            /* If we get here, the provider didn't throw an exception. 
             * This means the state has successfuly changed. 
             * Reset the internal state of this connection now its state has changed. */
            this.deletedUniqueIDs.Clear();
        }

        private PopResponse RSET()
        {
            /* Clear the list of deleted messages. (But keep the loaded list of unique-IDs.) */
            deletedUniqueIDs.Clear();

            /* Repot success. */
            return PopResponse.OKSingle("Un-flagged all messages flagged for delete.");
        }

        private PopResponse QAUT()
        {
            /* Only valid while logged in. */
            if (this.authenticated == false)
                return PopResponse.ERR("Not logged in.");

            /* Call the provider to finally delete the flagged messages. */
            DeleteFlaggedMessages(out int messageCount);
            this.deletedUniqueIDs.Clear();

            /* Leave the authenticated state. */
            this.unauthUserName = null;
            this.userNameAtLogin = null;
            this.authMailboxID = null;
            this.isSleeping = false;

            /* Report sucess. */
            return PopResponse.OKSingle("Logged out. You can either reauthenticate or exit.");
        }

        private PopResponse SLEE()
        {
            /* Reject if already sleeping. */
            if (this.isSleeping)
                return PopResponse.ERR("Already sleeping.");

            /* Call the provider to finally delete the flagged messages. */
            DeleteFlaggedMessages(out int messageCount);
            this.deletedUniqueIDs.Clear();

            /* Enter sleeing state. */
            this.isSleeping = true;

            /* Return success response. */
            return PopResponse.OKSingle($"Zzzzz. Deleted {messageCount} messages.");
        }

        private PopResponse WAKE()
        {
            /* Reject if not sleeping. */
            if (this.isSleeping == false)
                return PopResponse.ERR("Not sleeping.");

            /* Load a new set of unique IDs. */
            var newUniqueIDs = RefreshUniqueIDsFromMailbox();

            /* Check if there any new messages. */
            bool isNewMessages = newUniqueIDs.Except(this.uniqueIDs).Any();

            /* Store the new collection of uniqueIDs. */
            this.uniqueIDs = newUniqueIDs.ToList().AsReadOnly();

            /* Change back to awake state. */
            this.isSleeping = false;

            /* Return success. */
            return PopResponse.OKSingle(ActivityResponseCode(isNewMessages), "Welcome back.");
        }

        private string ActivityResponseCode(bool isNewMessages)
            => "ACTIVITY/" + (isNewMessages ? "NEW" : "NONE");

        private PopResponse NOOP()
            => PopResponse.OKSingle("There's no-one here but us POP3 services.");        


        private void ParseForUniqueId(string id, out int messageID, out string uniqueID)
        {
            /* If the id is a single string that parses, its a numeric message-id. */
            if (int.TryParse(id, out int parsedMessageID))
            {
                /* Reject bad message-ids. */
                if (parsedMessageID < 1 || parsedMessageID > this.uniqueIDs.Count)
                    throw new POP3ResponseException("No such message.");                

                /* Lookup the unique-ID from this message-ID. */
                messageID = parsedMessageID;
                uniqueID = this.uniqueIDs[messageID - 1];

                /* Check if this one has been flagged for delete. */
                if (string.IsNullOrEmpty(uniqueID) || deletedUniqueIDs.Contains(uniqueID))
                    throw new POP3ResponseException("That message has been deleted.");

                /* Otherwise, return to caller. */
                return;
            }

            /* Handle when the id starts "UID:". */
            if (id.ToUpperInvariant().StartsWith(UidParamPrefix))
            {
                /* Extract out everything after the colon. */
                string selectedUniqueID = id.Substring(UidParamPrefix.Length);

                /* Check it is a valid UID. */
                if (IsValidUniqueID(selectedUniqueID) == false)
                    throw new POP3ResponseException("UID/NOT-FOUND", "No such UID.");

                /* Check if this one has been flagged for delete. */
                if (string.IsNullOrEmpty(selectedUniqueID) || deletedUniqueIDs.Contains(selectedUniqueID))
                    throw new POP3ResponseException("That message has been deleted.");

                /* Look for message-id for this unique-id. */
                foreach (int loopedMessageID in Enumerable.Range(1, uniqueIDs.Count))
                {
                    /* Is this it? */
                    if (selectedUniqueID == uniqueIDs[loopedMessageID - 1])
                    {
                        messageID = loopedMessageID;
                        uniqueID = selectedUniqueID;
                        return;
                    }
                }

                /* Handle the new-message case? */
                if (this.service.AllowUnknownIDRequests)
                {
                    /* Does the requested ID exist on a fresh copy of the list of messages? */
                    if (service.Events.OnMessageList(authMailboxID).Contains(selectedUniqueID))
                    {
                        messageID = 0;
                        uniqueID = selectedUniqueID;
                        return;
                    }
                }

                /* No such message. */
                throw new POP3ResponseException("UID/NOT-FOUND", "No such UID.");
            }

            /* Unknown parameters. */
            throw new POP3ResponseException("Bad parameters.");
        }

        private bool IsValidUniqueID(string uniqueID)
        {
            /* Null/empty is not valid. */
            if (string.IsNullOrEmpty(uniqueID))
                return false;

            /* All characters must fall within '!' to '~' ASCII. */
            foreach (char ch in uniqueID)
            {
                int ascii = (int)ch;
                if (ascii < 33 || ascii > 126)
                    return false;
            }

            /* Passed test. */
            return true;
        }

    }
}

