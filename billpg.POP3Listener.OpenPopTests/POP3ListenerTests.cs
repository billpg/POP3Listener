/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using billpg.pop3;
using billpg.pop3.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPop.Pop3;

namespace OpenPopTests
{
    [TestClass]
    public class POP3ListenerTests
    {

        [TestMethod]
        public void POP3Listener_WithOpenPop()
        {
            /* Open a POP3 service. */
            using (POP3Listener listener = new POP3Listener())
            {
                var utprov = new UnitTestPOP3Provider();
                listener.OnAuthenticate = utprov.OnAuthenticateRequest;
                listener.OnListMailbox = utprov.OnListMailboxRequest;
                listener.OnMessageRetrieval = utprov.OnRetrieveRequest;
                listener.ListenOnStandard(IPAddress.Loopback);

                /* Collection of deleted messages. */
                var deletedUniqueIDs = new List<string>();

                /* Open a client. */
                using (var pop3 = new Pop3Client())
                {
                    pop3.Connect("localhost", 110, false);

                    /* Download capabilities. */
                    var capa = pop3.Capabilities();
                    CollectionAssert.Contains(capa.Keys, "USER");
                    CollectionAssert.Contains(capa.Keys, "UIDL");

                    /* Should not contain STLS because a TLS cert was supplied. */
                    CollectionAssert.DoesNotContain(capa.Keys, "STLS");

                    /* Log-in. */
                    pop3.Authenticate("me", "passw0rd");

                    /* Download the UIDs. Check all are present. */
                    var uids = pop3.GetMessageUids().ToList();
                    Assert.AreEqual(100, uids.Count);

                    /* Check the single-UID for a message works. */
                    var oneuid = pop3.GetMessageUid(84);
                    Assert.AreEqual(uids[84 - 1], oneuid);

                    /* Select ten random messages... */
                    foreach (var messageIndex in RandomNoRepeat(10, uids.Count))
                    {
                        /* Find the message-id and unique-id. */
                        int messageID = messageIndex + 1;
                        string uidCurrent = uids[messageIndex];

                        /* Download the message and check the contents. */
                        var msg = pop3.GetMessage(messageID);
                        Assert.AreEqual("me@example.com", msg.Headers.From.Address);
                        Assert.IsTrue(msg.MessagePart.GetBodyAsText().Contains(uidCurrent));

                        /* Flag the message to be deleted on exit. */
                        pop3.DeleteMessage(messageID);
                        deletedUniqueIDs.Add(uidCurrent);
                    }                   
                } /* Dispose, will call QUIT to commit the message-delete. */

                /* Log back in and check there are only 90 now. */
                using (var pop3 = new Pop3Client())
                {
                    pop3.Connect("localhost", 110, false);
                    var capa = pop3.Capabilities();
                    pop3.Authenticate("me", "passw0rd");
                    var uids = pop3.GetMessageUids().ToList();
                    Assert.AreEqual(90, uids.Count);
                    foreach (var expectedDeleted in deletedUniqueIDs)
                        CollectionAssert.DoesNotContain(uids, expectedDeleted);
                }
            }
        }

        private IEnumerable<int> RandomNoRepeat(int count, int max)
        {
            if (count > max)
                throw new ApplicationException("Called RandomNoRepeat with count greater than max.");

            HashSet<int> used = new HashSet<int>();
            Random rnd = new Random();
            for (int counter=0; counter<count; counter++)
            {
                int candidate = -1;
                while (true)
                {
                    candidate = rnd.Next(max);
                    if (used.Contains(candidate) == false)
                        break;
                }
                used.Add(candidate);
                yield return candidate;
            }
        }

        [TestMethod]
        public void POP3Listener_TLS()
        {
            using (POP3Listener listener = new POP3Listener())
            {
                var utprov = new UnitTestPOP3Provider();
                listener.OnAuthenticate = utprov.OnAuthenticateRequest;
                listener.OnListMailbox = utprov.OnListMailboxRequest;
                listener.ListenOnStandard(IPAddress.Loopback);
                listener.SecureCertificate = UnitTestPOP3Provider.selfSigned;

                using (var pop3 = new Pop3Client())
                {                    
                    pop3.Connect("localhost", 995, true, 1000, 1000, UnitTestPOP3Provider.CheckCert);
                    var capa = pop3.Capabilities();
                    CollectionAssert.Contains(capa.Keys, "X-TLS");
                    Assert.AreEqual("True", capa["X-TLS"].Single());
                }
            }
        }

    }
}

