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
            /* Launch POP3. */
            var pop3 = new POP3Listener();

            /* INSERT EVENT HANDLER CODE HERE. */

            /* Set our custom authentication function. */
            const string myMailboxID = "My-Authenticated-Mailbox-ID";
            pop3.Events.OnAuthenticate = MyAuthentictionHandler;
            void MyAuthentictionHandler(POP3AuthenticationRequest request)
            {
                /* Is this the only valid username and password? */
                if (request.SuppliedUsername == "me" && request.SuppliedPassword == "passw0rd")
                {
                    /* It is. Pass back the user's authenticated mailbox ID back to the server. */
                    request.AuthMailboxID = myMailboxID;
                }
            }

            /* Create (if needed) a folder to monitor for messages. */
            string mailboxFolder = Path.Combine(Path.GetTempPath(), "MyMailboxFolder");
            Directory.CreateDirectory(mailboxFolder);
            Console.WriteLine($"Mailbox: {mailboxFolder}");

            /* Set our custom function that returns a list of messages for a mailbox. */
            pop3.Events.OnMessageList = MyMessageList;
            IEnumerable<string> MyMessageList(string mailboxID)
            {
                /* Check mailbox ID. */
                if (mailboxID != myMailboxID)
                    throw new ApplicationException("Invalid mailbox ID.");

                /* Return just the filenames for each file in the folder. */
                return Directory.EnumerateFiles(mailboxFolder).Select(Path.GetFileName);
            }

            /* Set a function that returns the message contents. */
            pop3.Events.OnMessageRetrieval = MyMessageDownload;
            void MyMessageDownload(POP3MessageRetrievalRequest request)
            {
                /* Check mailbox ID. */
                if (request.AuthMailboxID != myMailboxID)
                    throw new ApplicationException("Invalid mailbox ID.");

                /* Locate the EML file in the mailbox folder. */
                string emlPath = Path.Combine(mailboxFolder, request.MessageUniqueID);

                /* Check file exists. */
                if (File.Exists(emlPath) == false)
                    throw new POP3ResponseException("Message has been expunged.");
            
                /* Pass the EML file to the server but don't delete it. */
                request.UseTextFile(emlPath, false);
            }

            /* Set a custom function that deletes a block of messages. */
            pop3.Events.OnMessageDelete = MyMessageDelete;
            void MyMessageDelete(string mailboxID, IList<string> messagesToDelete)
            {
                /* Check mailbox ID. */
                if (mailboxID != myMailboxID)
                    throw new ApplicationException("Invalid mailbox ID.");

                /* Delete each message one at a time, 
                 * glibly ignoring the principle of an atomic operation. */
                foreach (string messageToDelete in messagesToDelete)
                    File.Delete(Path.Combine(mailboxFolder, messageToDelete));
            }

            /* Replace the default size-of-message handler with an optimized one. */
            pop3.Events.OnMessageSize = MyMessageSize;
            long MyMessageSize(string mailboxID, string messageUniqueID)
            {
                /* Check mailbox ID. */
                if (mailboxID != myMailboxID)
                    throw new ApplicationException("Invalid mailbox ID.");

                /* Load the FileInfo object for the message and return the size. */
                return new FileInfo(Path.Combine(mailboxFolder, messageUniqueID)).Length;
            }

            /* Start listening. */
            pop3.ListenOn(IPAddress.Loopback, 110, false);

            /* Keep running until the process is killed. */
            while (true) System.Threading.Thread.Sleep(10000);
        }
    }
}