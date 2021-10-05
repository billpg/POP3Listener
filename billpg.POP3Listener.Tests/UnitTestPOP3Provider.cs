/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace billpg.pop3.Tests
{
    internal class UnitTestPOP3Provider : IPOP3Mailbox
    {
        public string Name => "UnitTest";
        internal List<string> uniqueIdsInMailbox;
        public RaiseNewMessageEvent onNewMessage;

        internal UnitTestPOP3Provider()
        {
            uniqueIdsInMailbox = new List<string>();
            for (int i = 0; i < 100; i++)
                uniqueIdsInMailbox.Add($"UID_{Guid.NewGuid():N}".ToUpperInvariant());
        }

        public void RegisterNewMessageAction(RaiseNewMessageEvent onNewMessage)
            => this.onNewMessage = onNewMessage;

        public IPOP3Mailbox Authenticate(IPOP3ConnectionInfo info, string user, string pass)
        {
            return (user == "me" && pass == "passw0rd") ? this : null;
        }

        internal void OnAuthenticateRequest(POP3AuthenticationRequest req)
        {
            if (req.SuppliedUsername == "me" && req.SuppliedPassword == "passw0rd")
                req.AuthUserID = "me-as-user-id";
            req.MailboxProvider = this;
        }

        internal IEnumerable<string> OnListMailboxRequest(string userID)
        {
            if (userID == "me-as-user-id")
                return uniqueIdsInMailbox.ToList();
            else
                return Enumerable.Empty<string>();
        }

        public string UserID(IPOP3ConnectionInfo info)
            => "UnitTestUserID";

        public bool MessageExists(IPOP3ConnectionInfo info, string uniqueID)
            => uniqueIdsInMailbox.Contains(uniqueID);

        public void OnRetrieveRequest(POP3MessageRetrievalRequest request)
        {
            Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.Contains(this.uniqueIdsInMailbox, request.MessageUniqueID);

            var msg = new List<string> 
            {
                "Subject: With love.",
                "From: me@example.com",
                "To: you@example.com",
                "",
                $"Unique id: {request.MessageUniqueID}",
                "",
                ". One dot.",
                ".. Two dots.",
                "... Three dots."               
            };

            request.UseLines(msg);
        }


        public void MessageDelete(IPOP3ConnectionInfo info, IList<string> uniqueIDs)
        {
            foreach (string uidToDelete in uniqueIDs)
                uniqueIdsInMailbox.Remove(uidToDelete);
        }

        internal readonly static X509Certificate selfSigned =
            new X509Certificate2(
                    LocateSelfSigned(),
                    "Rutabaga");

        private static string LocateSelfSigned()
        {
            var dir = new System.IO.DirectoryInfo(Environment.CurrentDirectory);
            while (true)
            {
                var dirNext = dir.GetDirectories("Pop3ServiceForm").SingleOrDefault();
                if (dirNext != null)
                {
                    return dirNext.GetFiles("SelfSigned.pfx").Single().FullName;
                }

                dir = dir.Parent;
            }

        }

        internal static bool CheckCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return certificate.ThumbprintSHA256Base64() == selfSigned.ThumbprintSHA256Base64();
        }

        private class WrapList : IMessageContent
        {
            private readonly List<string> msg;
            private int nextLineIndex;

            public WrapList(List<string> msg)
            {
                this.msg = msg;
                this.nextLineIndex = 0;
            }

            void IMessageContent.Close() { /* Nothing to do. */ }

            string IMessageContent.NextLine()
            {
                if (nextLineIndex < msg.Count)
                    return msg[nextLineIndex++];
                else
                    return null;
            }
        }
    }
}

