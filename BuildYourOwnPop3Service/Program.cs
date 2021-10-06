using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using billpg.pop3;

namespace BuildYourOwnPop3Service
{
    class Program
    {
        static void Main()
        {
            /* Create POP3 Server. */
            var pop3 = new POP3Listener();

            /* Set authentication provider. */
            pop3.Events.OnAuthenticate = MyAuth;
            void MyAuth(POP3AuthenticationRequest req)
            {
                if (req.SuppliedUsername == "me" && req.SuppliedPassword == "me")
                {
                    /* This User ID is authenticated. */
                    req.AuthMailboxID = "me-as-an-auth-user-ID";
                }
            }

            /* Set mailbox list provider. */
            pop3.Events.OnMessageList = MyListMailbox;
            IEnumerable<string> MyListMailbox(string mailboxID)
            {
                yield return "a";
                yield return "b";
                yield return "c";
            }

            /* Set message retrival. */
            pop3.Events.OnMessageRetrieval = MyRetrieve;
            void MyRetrieve(POP3MessageRetrievalRequest req)
            {
                req.UseLines(new List<string> 
                {
                    "Subject: With Love",
                    "From: me@example.com",
                    "To: you@example.com",
                    "",
                    "Da da dah, da da dum dum dah."
                });
            }

            /* Start it listening. */
            pop3.ListenOn(IPAddress.Loopback, 110, false);

            /* Keep running until the process is killed. */
            while (true) System.Threading.Thread.Sleep(10000);
        }
    }
}

#if false
    /* New class separate from the Program class. */
    class MyProvider : IPOP3MailboxProvider
    {
        /* Inside the MyProvider class. */
        public string Name => "My Provider";

        public IPOP3Mailbox Authenticate(
            IPOP3ConnectionInfo info,
            string username,
            string password)
        {
            if (username == "me" && password == "passw0rd")
                return new MyMailbox();
            else
                return null;
        }

        /* This is necessary, but we can ignore it. */
        public void RegisterNewMessageAction(
            RaiseNewMessageEvent onNewMessage)
        { }
    }

    class MyMailbox : IPOP3Mailbox
    {
        public string MailboxID(IPOP3ConnectionInfo info)
    => "Mr Rutabaga";

        const string FOLDER = @"C:\MyMailbox\";

        public IList<string> ListMessageUniqueIDs(
            IPOP3ConnectionInfo info)
            => Directory.GetFiles(FOLDER)
                   .Select(Path.GetFileName)
                   .ToList();

        public bool MessageExists(
            IPOP3ConnectionInfo info,
            string uniqueID)
            => ListMessageUniqueIDs(info)
                   .Contains(uniqueID);

        public bool MailboxIsReadOnly(
    IPOP3ConnectionInfo info)
    => false;



        public long MessageSize(
   IPOP3ConnectionInfo info,
   string uniqueID)
   => 58;

        /* Replace the MessageContents function. */
        public IMessageContent MessageContents(
            IPOP3ConnectionInfo info,
            string uniqueID)
        {
            if (MessageExists(info, uniqueID))
                return new MyMessageContents(
                               Path.Combine(FOLDER, uniqueID));
            else
                return null;
        }

        public void MessageDelete(
     IPOP3ConnectionInfo info,
     IList<string> uniqueIDs)
        {
            foreach (var toDelete in uniqueIDs)
                if (MessageExists(info, toDelete))
                    File.Delete(Path.Combine(FOLDER, toDelete));
        }

    }


    /* New class. */
    class MyMessageContents : IMessageContent
    {
        List<string> lines;
        int index;

        public MyMessageContents(string path)
        {
            lines = File.ReadAllLines(path).ToList();
            index = 0;
        }

        public string NextLine()
            => (index < lines.Count) ? lines[index++] : null;

        public void Close()
        {
        }
    }
#endif
