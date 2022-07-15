/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace billpg.pop3.Tests
{

    [TestClass]
    public partial class Pop3ListenerTestsBySocket
    {
        internal static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);
        internal static readonly IEnumerable<bool> falseAndTrue = new List<bool>(){ false, true }.AsReadOnly();

        private int port995 => 9955;
        private int port110 => 1100;

        [TestMethod]
        public void POP3_QAUT()
        {
            /* Construct a listener with a mailbox. */
            using var pop3 = new POP3Listener();
            pop3.Events.OnAuthenticate = req => req.AuthMailboxID = "x";
            var mailbox = Enumerable.Range(11, 100-11).Select(i => $"U{i}").ToHashSet();
            pop3.Events.OnMessageList = id => mailbox.Shuffle().Take(10);
            pop3.Events.OnMessageSize = (boxID, messageID) => 12;
            pop3.Events.OnMessageDelete = (boxID, messageIDs) => mailbox.RemoveWhere(messageIDs.Contains);

            /* Start listening. */
            int port = pop3.ListenOnRandom(IPAddress.Loopback, false);

            /* Connect as a client. */
            using var tcp = new TcpClient("localhost", port);
            using var str = tcp.GetStream();

            /* Read connect banner. */
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));

            /* Log-in. */
            str.WriteLine("USER This doesn't matter.");
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));
            str.WriteLine("PASS Nor this");
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));

            /* Mailbox contents. */
            str.WriteLine("UIDL");
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));
            var uidlResp = new List<string>();
            while(true)
            {
                var uidlLine = str.ReadLine();
                if (uidlLine == ".")
                    break;
                uidlResp.Add(uidlLine);
            }
            Assert.AreEqual(10, uidlResp.Count);

            /* Delete them all. */
            foreach (int messageNumber in Enumerable.Range(1, uidlResp.Count))
            {
                str.WriteLine($"DELE {messageNumber}");
                Assert.IsTrue(str.ReadLine().StartsWith("+OK"));
            }

            /* Log-out. Check messages are deleted before and after. */
            Assert.AreEqual(89, mailbox.Count);
            str.WriteLine("QAUT");
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));
            Assert.AreEqual(79, mailbox.Count);
            foreach (var deletedID in uidlResp)
                Assert.IsFalse(mailbox.Contains(deletedID));

            /* Not logged in. Various commands should fail. */
            foreach (var failCommand in "SLEE/WAKE/STAT/LIST/UIDL/RETR 1/PASS pass/DELE 1/QAUT".Split('/'))
            {
                str.WriteLine(failCommand);
                var respLine = str.ReadLine();
                if (failCommand == "PASS pass")
                    Assert.AreEqual("-ERR Call USER before PASS.", respLine);
                else
                    Assert.AreEqual("-ERR Authenticate first.", respLine);
            }

            /* Log in again. */
            str.WriteLine("XLOG Nothing really matters, anyone can see.");
            Assert.IsTrue(str.ReadLine().StartsWith("+OK"));

            /* Check STAT is correct. */
            str.WriteLine("STAT");
            Assert.AreEqual("+OK 10 120", str.ReadLine());
        }

        [TestMethod]
        public void POP3_ExcessiveMessageList()
        {
            /* Construct a listener. */
            POP3Listener pop3 = new POP3Listener();
            pop3.Events.OnAuthenticate = req => req.AuthMailboxID = "x";

            /* Set up a mailbox-list handler that updates a counter each time it is called. */
            int messageListInvokedCount = 0;
            pop3.Events.OnMessageList = IncrementCounter;
            IEnumerable<string> IncrementCounter(string mailboxID)
            {
                System.Threading.Interlocked.Increment(ref messageListInvokedCount);
                return "a,b,c,d".Split(',');
            }

            /* Set up a message size handler that returns the single letter of the uniqueID's asciii code. */
            pop3.Events.OnMessageSize = MyMessageSize;
            long MyMessageSize(string mailboxID, string uniqueID)
                => uniqueID.Single();

            /* Connect to the server. */
            pop3.ListenOn(IPAddress.Loopback, port110, false);
            try
            {
                using (var tcp = new TcpClient("localhost", port110))
                {
                    using (var str = tcp.GetStream())
                    {
                        /* Read welcome banner. */
                        var banner = ReadLine(str);
                        Assert.AreEqual("+OK POP3 service by billpg industries https://billpg.com/POP3/", banner);

                        /* Login. Check message list has been called exactly once. */
                        WriteLine(str, "XLOG x x");
                        var xlogResp = ReadLine(str);
                        Assert.AreEqual("+OK Welcome.", xlogResp);
                        Assert.AreEqual(1, messageListInvokedCount);

                        /* Call UIDL. */
                        WriteLine(str, "UIDL");
                        var uidlResp = string.Join("/", ReadMultiLineIfOkay(str));
                        Assert.AreEqual("+OK Unique-IDs follow.../1 a/2 b/3 c/4 d", uidlResp);
                        Assert.AreEqual(1, messageListInvokedCount);

                        /* Download a message. */
                        WriteLine(str, "LIST 1");
                        var retr1Resp = ReadLine(str);
                        Assert.AreEqual("+OK 1 97", retr1Resp);
                        Assert.AreEqual(1, messageListInvokedCount);

                        /* Download a message by UID that the server already knows about. */
                        WriteLine(str, "LIST UID:c");
                        var retr2resp = ReadLine(str);
                        Assert.AreEqual("+OK 3 99", retr2resp);
                        Assert.AreEqual(1, messageListInvokedCount);

                        /* Now request a UID that doesn't exist. */
                        WriteLine(str, "LIST UID:z");
                        var retr3resp = ReadLine(str);
                        Assert.AreEqual("-ERR [UID/NOT-FOUND] No such UID.", retr3resp);
                        Assert.AreEqual(2, messageListInvokedCount);
                    }
                }
            }
            finally
            {
                pop3.Stop();
            }
        }

        [TestMethod]
        public void POP3_SLEE_WAKE()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Loop many time, covering different action in the course of the connection. */
                foreach (bool externalDeleteMassge in falseAndTrue)
                    foreach (bool addMessage in falseAndTrue)
                        foreach (bool deleMessage in falseAndTrue)
                        {
                            /* Delete message using DELE. */
                            string selectedDele = prov.uniqueIdsInMailbox[50];
                            if (deleMessage)
                            {
                                WriteLine(str, "DELE UID:" + selectedDele);
                                var deleResp = ReadLine(str);
                                Assert.AreEqual($"+OK Message UID:{selectedDele} flagged for delete on QUIT or SLEE.", deleResp);
                            }

                            /* Go to sleep. */
                            WriteLine(str, "SLEE");
                            var sleeResp = ReadLine(str);
                            if (deleMessage)
                                Assert.AreEqual("+OK Zzzzz. Deleted 1 messages.", sleeResp);
                            else
                                Assert.AreEqual("+OK Zzzzz. Deleted 0 messages.", sleeResp);

                            /* Did it remove the message? */
                            if (deleMessage)
                                Assert.IsFalse(prov.uniqueIdsInMailbox.Contains(selectedDele));

                            /* Some command don't work. */
                            WriteLine(str, "UIDL");
                            var uidlResp = ReadLine(str);
                            Assert.AreEqual("-ERR This command is not allowed in a sleeping state. (Only NOOP, QUIT or WAKE.)", uidlResp);

                            /* NOOP works. */
                            WriteLine(str, "NOOP");
                            var noopResp = ReadLine(str);
                            Assert.AreEqual("+OK There's no-one here but us POP3 services.", noopResp);

                            /* Delete or add a message during sleep. */
                            if (externalDeleteMassge)
                                prov.uniqueIdsInMailbox.RemoveAt(0);
                            if (addMessage)
                                prov.uniqueIdsInMailbox.Add($"{Guid.NewGuid()}");

                            /* Wake up! */
                            WriteLine(str, "WAKE");
                            var wakeResp = ReadLine(str);
                            if (addMessage)
                                Assert.AreEqual("+OK [ACTIVITY/NEW] Welcome back.", wakeResp);
                            else
                                Assert.AreEqual("+OK [ACTIVITY/NONE] Welcome back.", wakeResp);
                        }
            }
        }

        [TestMethod]
        public void POP3_TLS_995()
        {
            using (POP3Listener listener = new POP3Listener())
            {
                listener.Events.OnAuthenticate = new UnitTestPOP3Provider().OnAuthenticateRequest;
                listener.ListenOnHigh(IPAddress.Loopback);
                listener.SecureCertificate = UnitTestPOP3Provider.selfSigned;

                using (var tcp = new TcpClient())
                {
                    /* Connect to the secure port. */
                    tcp.Connect("localhost", port995);
                    var str = tcp.GetStream();

                    /* Hand over control to TLS. */
                    using (var tls = new SslStream(str, false, UnitTestPOP3Provider.CheckCert))
                    {
                        /* Authenticate wih the server. */
                        tls.AuthenticateAsClient("this.is.invalid");

                        /* Read the banner. */
                        string banner = ReadLine(tls);
                        Assert.IsTrue(banner.StartsWith("+OK "));

                        /* Read capabilities. */
                        WriteLine(tls, "CAPA");
                        var capaResp1 = ReadMultiLineIfOkay(tls);
                        CollectionAssert.Contains(capaResp1, "X-TLS True");
                    }
                }
            }
        }

        [TestMethod]
        public void POP3_STLS_PostInjection()
        {
            using (POP3Listener listener = new POP3Listener())
            {
                listener.Events.OnAuthenticate = new UnitTestPOP3Provider().OnAuthenticateRequest;
                listener.ListenOnHigh(IPAddress.Loopback);
                listener.SecureCertificate = UnitTestPOP3Provider.selfSigned;

                using (var tcp = new TcpClient())
                {
                    /* Connect to the normal insecure port. */
                    tcp.Connect("localhost", port110);
                    var str = tcp.GetStream();

                    /* Read the banner. */
                    string banner = ReadLine(str);
                    Assert.IsTrue(banner.StartsWith("+OK "));

                    /* Send STLS with an extra unauthorised command. */
                    WriteLine(str, "STLS\r\nUSER doer-of-evil\r\nPASS Mwahahaha!");
                    var respStls = ReadLine(str);
                    Assert.IsTrue(respStls.StartsWith("+OK "));

                    /* Hand over to TLS. */
                    using (var tls = new SslStream(str, false, UnitTestPOP3Provider.CheckCert))
                    {
                        tls.AuthenticateAsClient("this.is.invalid");

                        /* Send a command inside the TLS channel. The respoonse should be to CAPA, not USER. */
                        WriteLine(tls, "CAPA");
                        var respCapa = ReadMultiLineIfOkay(tls);
                        Assert.IsTrue(respCapa.First().StartsWith("+OK Capa"));
                    }
                }
            }
        }

        [TestMethod]
        public void POP3_STLS_110()
        {
            using (POP3Listener listener = new POP3Listener())
            {
                listener.Events.OnAuthenticate = new UnitTestPOP3Provider().OnAuthenticateRequest;
                listener.ListenOnHigh(IPAddress.Loopback);
                listener.SecureCertificate = UnitTestPOP3Provider.selfSigned;

                using (var tcp = new TcpClient())
                {
                    /* Connect to the normal insecure port. */
                    tcp.Connect("localhost", port110);
                    var str = tcp.GetStream();

                    /* Read the banner. */
                    string banner = ReadLine(str);
                    Assert.IsTrue(banner.StartsWith("+OK "));

                    /* Send a CAPA. */
                    WriteLine(str, "CAPA");
                    var respCapa = ReadMultiLineIfOkay(str);
                    Assert.IsTrue(respCapa.First().StartsWith("+OK "));
                    Assert.IsTrue(respCapa.Contains("X-TLS False"));
                    Assert.IsTrue(respCapa.Contains("STLS"));

                    /* Send STLS */
                    WriteLine(str, "STLS");
                    var respStls = ReadLine(str);
                    Assert.IsTrue(respStls.StartsWith("+OK "));

                    /* Hand over control to TLS. */
                    using (var tls = new SslStream(str, false, UnitTestPOP3Provider.CheckCert))
                    {
                        tls.AuthenticateAsClient("this.is.invalid");

                        /* Repeat the CAPA request. */
                        WriteLine(tls, "CAPA");
                        var respCapa2 = ReadMultiLineIfOkay(tls);
                        Assert.IsTrue(respCapa2.First().StartsWith("+OK "));
                        Assert.IsTrue(respCapa2.Contains("X-TLS True"));
                        Assert.IsFalse(respCapa2.Contains("STLS"));
                    }
                }
            }
        }

        private void RunTestLoggedIn(Action<Stream,UnitTestPOP3Provider> test)
        {
            using (POP3Listener listener = new POP3Listener())
            {
                var prov = new UnitTestPOP3Provider();
                listener.Events.OnAuthenticate = prov.OnAuthenticateRequest;
                listener.Events.OnMessageList = prov.OnMessageListRequest;
                listener.Events.OnMessageRetrieval = prov.OnRetrieveRequest;
                listener.Events.OnMessageDelete = prov.OnDeleteRequest;
                listener.ListenOnHigh(IPAddress.Loopback);

                using (var tcp = new TcpClient())
                {
                    /* Connect to the normal insecure port. */
                    tcp.Connect("localhost", port110);
                    var str = tcp.GetStream();

                    /* Read the banner. */
                    string banner = ReadLine(str);
                    Assert.IsTrue(banner.StartsWith("+OK "));

                    /* USER. */
                    WriteLine(str, "USER me");
                    string userResp = ReadLine(str);
                    Assert.IsTrue(userResp.StartsWith("+OK "));

                    /* PASS. */
                    WriteLine(str, "PASS passw0rd");
                    string passResp = ReadLine(str);
                    Assert.IsTrue(passResp.StartsWith("+OK "));

                    /* Hand the logged-in server to the specific unit test. */
                    test(str, prov);
                }
            }

        }

        [TestMethod]
        public void POP3_TOP()
        {
            /* Dovecot retruns the header+blank line for TOP n 0.
             * Returns header+blank+single for TOP n 1. */
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Request header + zero lines. */
                WriteLine(str, "TOP 1 0");
                var topResp1 = ReadMultiLineIfOkay(str);
                Assert.IsTrue(topResp1.First().StartsWith("+OK "));

                /* Remove the header and test remaining response. */
                RemoveHeader(topResp1);
                Assert.AreEqual(1, topResp1.Count);
                Assert.AreEqual("", topResp1.Single());

                /* Request header + one line. */
                WriteLine(str, "TOP 1 1");
                var topResp2 = ReadMultiLineIfOkay(str);
                Assert.IsTrue(topResp2.First().StartsWith("+OK "));

                /* Remove the deader and test expected both lines. */
                RemoveHeader(topResp2);
                Assert.AreEqual(2, topResp2.Count);
                Assert.AreEqual("", topResp2[0]);
                Assert.AreEqual($"Unique id: {prov.uniqueIdsInMailbox[0]}", topResp2[1]);

                /* Request header + all lines. */
                WriteLine(str, "TOP 1 99999");
                var topResp3 = ReadMultiLineIfOkay(str);
                Assert.IsTrue(topResp3.First().StartsWith("+OK "));

                /* Remove header and test lines. */
                RemoveHeader(topResp3);
                Assert.AreEqual(6, topResp3.Count);
                Assert.AreEqual("", topResp3[0]);
                Assert.AreEqual($"Unique id: {prov.uniqueIdsInMailbox[0]}", topResp3[1]);
                Assert.AreEqual("", topResp3[2]);
                Assert.AreEqual(".. One dot.", topResp3[3]);
                Assert.AreEqual("... Two dots.", topResp3[4]);
                Assert.AreEqual(".... Three dots.", topResp3[5]);
            }
        }

        private void RemoveHeader(List<string> resp)
        {
            while (resp.Any())
            {
                if (resp[0].Length == 0)
                    return;
                else
                    resp.RemoveAt(0);
            }
        }

        [TestMethod]
        public void POP3_CORE()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Loop through various combinations of number of messages to add vs number of messages to delete. */
                foreach (int countToAdd in Enumerable.Range(0, 5))
                    foreach (int countToDelete in Enumerable.Range(0, 5))
                        foreach (int counterToExternallyDelete in Enumerable.Range(0, countToDelete + 1))
                        {
                            /* Check the starting messages were enough. */
                            Assert.IsTrue(prov.uniqueIdsInMailbox.Count > countToDelete * 3);

                            /* Run STAT to set the base-line. */
                            int countBefore = prov.uniqueIdsInMailbox.Count;
                            WriteLine(str, "STAT");
                            var statRespBefore = ReadLine(str);
                            Assert.IsTrue(statRespBefore.StartsWith($"+OK {countBefore} "));

                            /* Loop through the messages to delete. */
                            List<string> deletedIDs = new List<string>();
                            int expectedDeletedByServer = 0;
                            foreach (int deleteCounter in Enumerable.Range(0, countToDelete))
                            {
                                /* Select a uniqueID exactly one third along. */
                                string deleteID = prov.uniqueIdsInMailbox[prov.uniqueIdsInMailbox.Count / 3 + deleteCounter];
                                deletedIDs.Add(deleteID);

                                /* Is this the one to extenrally delete? */
                                if (deleteCounter == counterToExternallyDelete)
                                {
                                    prov.uniqueIdsInMailbox.Remove(deleteID);
                                }

                                /* Others, delete by command. */
                                else
                                {
                                    WriteLine(str, "DELE UID:" + deleteID);
                                    var deleResp = ReadLine(str);
                                    Assert.AreEqual($"+OK Message UID:{deleteID} flagged for delete on QUIT or SLEE.", deleResp);
                                    expectedDeletedByServer++;
                                }
                            }

                            /* Add some messages as requested by caller. */
                            List<string> addedIDs = new List<string>();
                            foreach (int addCounter in Enumerable.Range(0, countToAdd))
                            {
                                string addedID = $"New-{addCounter}-{Guid.NewGuid()}";
                                prov.uniqueIdsInMailbox.Add(addedID);
                                addedIDs.Add(addedID);
                            }

                            /* Commit the deletes and go to sleep. */
                            WriteLine(str, "SLEE");
                            var sleeResp = ReadLine(str);
                            Assert.AreEqual($"+OK Zzzzz. Deleted {expectedDeletedByServer} messages.", sleeResp);

                            /* Refresh, checking new or no-new messages as expected. */
                            WriteLine(str, "WAKE");
                            var refrResp = ReadLine(str);
                            string expectedActvityCode = (countToAdd == 0) ? "NONE" : "NEW";
                            Assert.AreEqual($"+OK [ACTIVITY/{expectedActvityCode}] Welcome back.", refrResp);

                            /* Check all deleted messages have gone. */
                            foreach (string shouldBeDeletedID in deletedIDs)
                                CollectionAssert.DoesNotContain(prov.uniqueIdsInMailbox, shouldBeDeletedID);

                            /* Check all added are still present. */
                            foreach (string shouldBeAddedID in addedIDs)
                                CollectionAssert.Contains(prov.uniqueIdsInMailbox, shouldBeAddedID);

                            /* Run STAT again. The deleted messages should be missing and the new ones added. */
                            WriteLine(str, "STAT");
                            var statResp3 = ReadLine(str);
                            Assert.IsTrue(statResp3.StartsWith($"+OK {countBefore - countToDelete + countToAdd} "));
                        }
            }
        }

        static string UpToLastSpace(string x)
        {
            int lastSpaceIndex = x.LastIndexOf(' ');
            if (lastSpaceIndex < 0)
                return x;
            else
                return x.Substring(0, lastSpaceIndex);

        }

        [TestMethod]
        public void POP3_RETR_UID()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Collect the UIDs before for comparison later. */
                var oldUids = prov.uniqueIdsInMailbox.ToList();

                /* Request one of the UIDs without UIDL-ing first. */
                string uid = prov.uniqueIdsInMailbox[49];
                WriteLine(str, $"RETR UID:{uid}");
                var retrResp = ReadMultiLineIfOkay(str);
                Assert.IsTrue(retrResp.First().StartsWith("+OK "));
                Assert.IsTrue(retrResp.Any(line => line.Contains(uid)));

                /* Delete the message by UID. */
                WriteLine(str, $"DELE UID:{uid}");
                var deleResp = ReadLine(str);
                Assert.IsTrue(deleResp.StartsWith("+OK "));

                /* Not deleted it yet. */
                CollectionAssert.Contains(prov.uniqueIdsInMailbox, uid);

                /* Commit and sleep. */
                WriteLine(str, "SLEE");
                var sleeResp = ReadLine(str);
                Assert.IsTrue(sleeResp.StartsWith("+OK "));

                /* Wake up and refresh. */
                WriteLine(str, "WAKE");
                var wakeResp = ReadLine(str);
                Assert.IsTrue(wakeResp.StartsWith("+OK [ACTIVITY/NONE] "));

                /* Now deleted, but the others are still there. */
                CollectionAssert.DoesNotContain(prov.uniqueIdsInMailbox, uid);
                foreach (var oldUid in oldUids)
                    if (oldUid != uid)
                        CollectionAssert.Contains(prov.uniqueIdsInMailbox, oldUid);

                /* Exit. */
                WriteLine(str, "QUIT");
                var quitResp = ReadLine(str);
                Assert.IsTrue(quitResp.StartsWith("+OK "));
            }
        }

        [TestMethod]
        public void POP3_RETR_UID_New()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Collect the STAT report. */
                WriteLine(str, "STAT");
                var statResp1 = ReadLine(str);
                Assert.AreEqual("+OK 100 16000", statResp1);

                /* Obtain the list of unique-IDs. */
                WriteLine(str, "UIDL");
                var uidlFirst = ReadMultiLineIfOkay(str);
                Assert.AreEqual(101, uidlFirst.Count);

                /* Add a new unique id to the underlying collection. */
                string uniqueIDNew = $"NewUniqueID_{Guid.NewGuid():N}";
                prov.uniqueIdsInMailbox.Add(uniqueIDNew);

                /* Confirm the UIDL response is still the same. */
                WriteLine(str, "UIDL");
                var uidlSecond = ReadMultiLineIfOkay(str);
                CollectionAssert.AreEqual(uidlFirst, uidlSecond);

                /* Without calling refresh, download the new unique-id. */
                WriteLine(str, $"RETR UID:{uniqueIDNew}");
                var retrResp = ReadMultiLineIfOkay(str);
                Assert.AreEqual("+OK Message text follows...", retrResp.First());
                Assert.IsTrue(retrResp.Contains($"Unique id: {uniqueIDNew}"));

                /* But a made up Unique ID should fail. */
                WriteLine(str, $"RETR UID:{Guid.NewGuid():N}");
                var retrBadResp = ReadMultiLineIfOkay(str);
                Assert.AreEqual(1, retrBadResp.Count);
                Assert.AreEqual("-ERR [UID/NOT-FOUND] No such UID.", retrBadResp.Single());

                /* Test the TOP command with a new unique-id. */
                WriteLine(str, $"TOP UID:{uniqueIDNew} 2");
                var topResp = ReadMultiLineIfOkay(str);
                Assert.AreEqual(7, topResp.Count);
                Assert.IsTrue(topResp.Contains($"Unique id: {uniqueIDNew}"));

                /* The LIST UID command reveals a message's size. */
                WriteLine(str, $"LIST UID:{uniqueIDNew}");
                var listResp = ReadLine(str);
                Assert.AreEqual("+OK 0 168", listResp);

                /* The UIDL UID command is useless, but test it anyway. */
                WriteLine(str, $"UIDL UID:{uniqueIDNew}");
                var uidlSingleResp = ReadLine(str);
                Assert.AreEqual($"+OK 0 {uniqueIDNew}", uidlSingleResp);

                /* Delete the message without a message id? */
                WriteLine(str, $"DELE UID:{uniqueIDNew}");
                var deleOneResp = ReadLine(str);
                Assert.AreEqual($"+OK Message UID:{uniqueIDNew} flagged for delete on QUIT or SLEE.", deleOneResp);

                /* Delete a normal message too. */
                string normalUniqueIdToDelete = prov.uniqueIdsInMailbox[84];
                WriteLine(str, $"DELE UID:{normalUniqueIdToDelete}");
                var deleTwoResp = ReadLine(str);
                Assert.AreEqual($"+OK Message UID:{normalUniqueIdToDelete} flagged for delete on QUIT or SLEE.", deleTwoResp);

                /* Check the two selected uniqueIDs are still there. */
                Assert.IsTrue(prov.uniqueIdsInMailbox.Contains(uniqueIDNew));
                Assert.IsTrue(prov.uniqueIdsInMailbox.Contains(normalUniqueIdToDelete));
                Assert.AreEqual(101, prov.uniqueIdsInMailbox.Count);

                /* Can't use the flagged messages, even before they have been committed. */
                foreach (string flaggedUniqueID in new string[] { uniqueIDNew, normalUniqueIdToDelete })
                {
                    /* Try each command with a UID form. */
                    foreach (string command in new string[] { "RETR", "LIST", "UIDL" })
                    {
                        /* Expecting the specific message that this has been deleted. */
                        WriteLine(str, $"{command} UID:{flaggedUniqueID}");
                        var flaggedResp = ReadLine(str);
                        Assert.AreEqual("-ERR That message has been deleted.", flaggedResp);
                    }
                }

                /* Attempting to retreve by message-id should also fail. */
                string deletedMessageID = uidlFirst.Where(u => u.Contains(normalUniqueIdToDelete)).Single().Split(' ').First();
                WriteLine(str, "RETR " + deletedMessageID);
                var retr84Resp = ReadLine(str);
                Assert.AreEqual("-ERR That message has been deleted.", retr84Resp);

                /* Check STAT no-longer includes the deleted ones. */
                WriteLine(str, "STAT");
                var statrResp2 = ReadLine(str);
                Assert.AreEqual("+OK 99 15840", statrResp2);

                /* Commit the deletes and exit. */
                WriteLine(str, "QUIT");
                var quitResp = ReadLine(str);
                Assert.AreEqual("+OK 2 messages deleted. Closing connection.", quitResp);

                /* Check the two selected uniqueIDs are missing. */
                Assert.IsFalse(prov.uniqueIdsInMailbox.Contains(uniqueIDNew));
                Assert.IsFalse(prov.uniqueIdsInMailbox.Contains(normalUniqueIdToDelete));
                Assert.AreEqual(99, prov.uniqueIdsInMailbox.Count);
            }
        }

        [TestMethod]
        public void POP3_DELI()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Collect UIDL. */
                WriteLine(str, "UIDL");
                var uidl1 = ReadMultiLineIfOkay(str);
                Assert.AreEqual(101, uidl1.Count);

                /* Delete a message with a numeric ID. */
                string deletedID1 = prov.uniqueIdsInMailbox[11];
                WriteLine(str, "DELI 12");
                var deliResp1 = ReadLine(str);
                Assert.AreEqual($"+OK Deleted message. UID:{deletedID1}", deliResp1);
                Assert.AreEqual(99, prov.uniqueIdsInMailbox.Count);
                Assert.IsFalse(prov.uniqueIdsInMailbox.Contains(deletedID1));

                /* Perform another UIDL, expecting a gap at 12. */
                WriteLine(str, "UIDL");
                var uidl2 = ReadMultiLineIfOkay(str);
                Assert.AreEqual(100, uidl2.Count);
                Assert.IsTrue(uidl2.Where(l => l.StartsWith("11 ")).Any());
                Assert.IsFalse(uidl2.Where(l => l.StartsWith("12 ")).Any());
                Assert.IsTrue(uidl2.Where(l => l.StartsWith("13 ")).Any());
                Assert.AreEqual(uidl1[58], uidl2[57]);

                /* Delete by a known unique ID. */
                string deletedID2 = prov.uniqueIdsInMailbox[28];
                WriteLine(str, $"DELI UID:{deletedID2}");
                string deliResp2 = ReadLine(str);
                Assert.AreEqual($"+OK Deleted message. UID:{deletedID2}", deliResp2);

                /* UIDL again. */
                WriteLine(str, "UIDL");
                var uidl3 = ReadMultiLineIfOkay(str);
                Assert.AreEqual(99, uidl3.Count);
                Assert.IsTrue(uidl3.Where(l => l.StartsWith("11 ")).Any());
                Assert.IsFalse(uidl3.Where(l => l.StartsWith("12 ")).Any());
                Assert.IsTrue(uidl3.Where(l => l.StartsWith("13 ")).Any());
                Assert.IsTrue(uidl3.Where(l => l.StartsWith("29 ")).Any());
                Assert.IsFalse(uidl3.Where(l => l.StartsWith("30 ")).Any());
                Assert.IsTrue(uidl3.Where(l => l.StartsWith("31 ")).Any());
                Assert.AreEqual(uidl1[58], uidl3[56]);

                /* Add an ID behind the scenes. */
                string deletedID3 = $"{Guid.NewGuid():N}";
                prov.uniqueIdsInMailbox.Insert(0, deletedID3);

                /* Delete that unknown message by its ID. */
                WriteLine(str, $"DELI UID:{deletedID3}");
                string deliResp3 = ReadLine(str);
                Assert.AreEqual($"+OK Deleted message. UID:{deletedID3}", deliResp3);

                /* Check UIDL is the same. */
                WriteLine(str, "UIDL");
                var uidl4 = ReadMultiLineIfOkay(str);
                CollectionAssert.AreEqual(uidl3, uidl4);
            }
        }

        [TestMethod]
        public void POP3_CAPA()
        {
            RunTestLoggedIn(Internal);
            void Internal(Stream str, UnitTestPOP3Provider prov)
            {
                /* Collect CAPA. */
                WriteLine(str, "CAPA");
                var capa = ReadMultiLineIfOkay(str);

                /* Check the standard capabiltieies are present. */
                Assert.IsTrue(capa.Contains("USER"));
                Assert.IsTrue(capa.Contains("TOP"));
                Assert.IsTrue(capa.Contains("UIDL"));
                Assert.IsTrue(capa.Contains("RESP-CODES"));
                Assert.IsTrue(capa.Contains("PIPELINING"));
                Assert.IsTrue(capa.Contains("AUTH-RESP-CODE"));

                /* Check my ones (that I'm keeping) are too. */
                Assert.IsTrue(capa.Contains("SLEE-WAKE"));
                Assert.IsTrue(capa.Contains("UID-PARAM"));
                Assert.IsTrue(capa.Contains("DELI"));
            }
        }
    }

    [TestClass]
    public class GoodBadUniqueIDs
    {
        [TestMethod] public void POP3_UIDL_BadUniqueID_CRLF() => UniqueIDValidation(false, "\r\n");
        [TestMethod] public void POP3_UIDL_BadUniqueID_Empty() => UniqueIDValidation(false, "");
        [TestMethod] public void POP3_UIDL_BadUniqueID_Space() => UniqueIDValidation(false, " ");
        [TestMethod] public void POP3_UIDL_BadUniqueID_DEL() => UniqueIDValidation(false, $"{(char)127}");
        [TestMethod] public void POP3_UIDL_BadUniqueID_Underscore() => UniqueIDValidation(false, "_");
        [TestMethod] public void POP3_UIDL_BadUniqueID_Emoji() => UniqueIDValidation(false, "\uD83D\uDC18");
        [TestMethod] public void POP3_UIDL_BadUniqueID_Unicode() => UniqueIDValidation(false, "\u0418");
        [TestMethod] public void POP3_UIDL_BadUniqueID_71Chars() => UniqueIDValidation(false, new string('J', 71));

        [TestMethod] public void POP3_UIDL_GoodUniqueID_SSrSSn() => UniqueIDValidation(true, "\\r\\n");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_Quotes() => UniqueIDValidation(true, "\"\"");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_Excl() => UniqueIDValidation(true, "!");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_Tilde() => UniqueIDValidation(true, "~");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_ExclTidle() => UniqueIDValidation(true, "!~");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_2Underscores() => UniqueIDValidation(true, "__");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_SSHexSShex() => UniqueIDValidation(true, "\\uD83D\\uDC18");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_SSHex() => UniqueIDValidation(true, "\\u0418");
        [TestMethod] public void POP3_UIDL_GoodUniqueID_70Chars() => UniqueIDValidation(true, new string('!', 70));

        void UniqueIDValidation(bool expectedValid, params string[] uniqueIDs)
        {
            foreach (string badUniqueID in uniqueIDs)
            {
                using (POP3Listener listener = new POP3Listener())
                {
                    var prov = new UnitTestPOP3Provider();
                    prov.uniqueIdsInMailbox = new List<string> { badUniqueID };
                    listener.Events.OnAuthenticate = prov.OnAuthenticateRequest;
                    listener.Events.OnMessageList = prov.OnMessageListRequest;
                    listener.Events.OnCommandReceived = MyCommandRecv;
                    void MyCommandRecv(IPOP3ConnectionInfo info, string command)
                    {

                    }
                    int port = listener.ListenOnRandom(IPAddress.Loopback, false);

                    using (var tcp = new TcpClient())
                    {
                        /* Connect to the normal insecure port. */
                        tcp.Connect("localhost", port);
                        var str = tcp.GetStream();

                        /* Read the banner. */
                        string banner = str.ReadLine();
                        Assert.IsTrue(banner.StartsWith("+OK "));

                        /* USER. */
                        str.WriteLine("USER me");
                        string userResp = str.ReadLine();
                        Assert.IsTrue(userResp.StartsWith("+OK "));

                        /* PASS. The server should check the list of uniqueIDs here and reject if there are any bad ones. */
                        str.WriteLine("PASS passw0rd");
                        string passResp = str.ReadLine();
                        if (expectedValid)
                            Assert.AreEqual("+OK Welcome.", passResp);
                        else
                            Assert.AreEqual("-ERR Unique ID strings must be printable ASCII only.", passResp);
                    }
                }
            }
        }
    }

    partial class Pop3ListenerTestsBySocket
    {
        [TestMethod]
        public void POP3_QUIT()
        {
            using (var pop3 = new POP3Listener())
            {
                /* Start listening, allowing the OS to pick to the port. */
                int svcPort = pop3.ListenOnRandom(IPAddress.Loopback, false);
                Assert.IsTrue(svcPort > 1023);
                Assert.IsTrue(svcPort < 65536);

                /* Open a client. */
                using (var tcp = new TcpClient())
                {
                    /* Connect to the normal insecure port. */
                    tcp.Connect("localhost", svcPort);
                    var str = tcp.GetStream();

                    /* Read the banner. */
                    string banner = ReadLine(str);
                    Assert.IsTrue(banner.StartsWith("+OK "));

                    /* Send a QUIT command. */
                    str.WriteLine("QUIT");

                    /* Read the response. */
                    string quitResp = ReadLine(str);
                    Assert.AreEqual("+OK Closing connection without authenticating.", quitResp);

                    /* Confirm the stream is closed. */
                    Assert.AreEqual(-1, str.ReadByte());
                }
            }
        }
    }

    [TestClass]
    public class DeliberatelyThrowExceptions
    {
        [TestMethod]
        public void POP3_AuthThrowsAppException()
            => WithThrowInternal("auth", new ApplicationException("Oops"), "-ERR [SYS/TEMP] System error. Administrators should check logs.", null);

        [TestMethod]
        public void POP3_AuthThrowsPopRespException()
            => WithThrowInternal("auth", new POP3ResponseException("Oopsy"), "-ERR Oopsy", "-ERR Authenticate first.");

        [TestMethod]
        public void POP3_AuthThrowsPopRespExceptionWithCode()
            => WithThrowInternal("auth", new POP3ResponseException("UNIT/TEST","Oopsy doopsy!"), "-ERR [UNIT/TEST] Oopsy doopsy!", "-ERR Authenticate first.");

        [TestMethod]
        public void POP3_ListThrowsAppException()
            => WithThrowInternal("list", new ApplicationException("Oops"), "-ERR [SYS/TEMP] System error. Administrators should check logs.", null);

        [TestMethod]
        public void POP3_ListThrowsPopRespException()
            => WithThrowInternal("list", new POP3ResponseException("Oopsy"), "-ERR Oopsy", "-ERR [SYS/TEMP] System error. Administrators should check logs.");

        [TestMethod]
        public void POP3_ListThrowsPopRespExceptionWithCode()
            => WithThrowInternal("list", new POP3ResponseException("UNIT/TEST", "Oopsy doopsy!"), "-ERR [UNIT/TEST] Oopsy doopsy!", "-ERR [SYS/TEMP] System error. Administrators should check logs.");

        [TestMethod]
        public void POP3_SizeThrowsAppException()
            => WithThrowInternal("size", new ApplicationException("Oops"), "+OK Welcome.", "-ERR [SYS/TEMP] System error. Administrators should check logs.");

        [TestMethod]
        public void POP3_SizeThrowsPopRespException()
            => WithThrowInternal("size", new POP3ResponseException("Oopsy"), "+OK Welcome.", "-ERR Oopsy");

        [TestMethod]
        public void POP3_SizeThrowsPopRespExceptionWithCode()
            => WithThrowInternal("size", new POP3ResponseException("UNIT/TEST", "Oopsy doopsy!"), "+OK Welcome.", "-ERR [UNIT/TEST] Oopsy doopsy!");

        private void WithThrowInternal(string mode, Exception ex, string expectedPassResp, string expectedStatResp)
        {
            /* Start listening. All autentication requests throw errors. */
            using var pop3 = new POP3Listener();
            if (mode == "auth")
            {
                pop3.Events.OnAuthenticate = x => throw ex;
            }
            else if (mode == "list")
            {
                pop3.Events.OnAuthenticate = x => x.AuthMailboxID = "x";
                pop3.Events.OnMessageList = x => throw ex;
            }
            else if (mode == "size")
            {
                pop3.Events.OnAuthenticate = x => x.AuthMailboxID = "x";
                pop3.Events.OnMessageList = x => new List<string> { "a" };
                pop3.Events.OnMessageSize = (x,y) => throw ex;
            }

            /* Start listening. */
            int svcPort = pop3.ListenOnRandom(IPAddress.Loopback, false);

            /* Connect. */
            using var tcp = new TcpClient();
            tcp.Connect("localhost", svcPort);
            using var str = tcp.GetStream();

            /* Read the banner. */
            string banner = str.ReadLine();
            Assert.IsTrue(banner.StartsWith("+OK "));

            /* Send USER command, expecting a +OK response. */
            str.WriteLine("USER itsame");
            string userResp = str.ReadLine();
            Assert.AreEqual("+OK Thank you. Send password.", userResp);

            /* Send PASS command, expecting a -ERR response. */
            str.WriteLine("PASS mario!");
            string passResp = str.ReadLine();
            Assert.AreEqual(expectedPassResp, passResp);

            /* Stop test if null stat response. */
            if (expectedStatResp == null)
            {
                Assert.AreEqual(10, str.ReadByte());
                Assert.AreEqual(-1, str.ReadByte());
                return;
            }

            /* Send STAT command. */
            str.WriteLine("STAT");
            string statResp = str.ReadLine();
            Assert.AreEqual(expectedStatResp, statResp);
        }
    }

    partial class Pop3ListenerTestsBySocket
    {
        private List<string> ReadMultiLineIfOkay(Stream str)
        {
            /* Read the first line. */
            string first = ReadLine(str);

            /* If -ERR, return on its own. */
            if (first.StartsWith("-ERR "))
                return new List<string> { first };

            /* If +OK, megin mltiline. */
            else if (first.StartsWith("+OK "))
            {
                List<string> resp = new List<string> { first };
                while (true)
                {
                    string next = ReadLine(str);
                    if (next == ".")
                        return resp;

                    resp.Add(next);
                }
            }

            else
                throw new ApplicationException("Unknown response: " + first);
        }

        private string ReadLine(Stream str)
        {
            /* Collect bytes until LF. */
            List<byte> lineBytes = new List<byte>();
            while (true)
            {
                int byteRead = str.ReadByte();
                if (byteRead < 0 || byteRead == 10)
                    break;
                lineBytes.Add((byte)byteRead);
            }
            return Encoding.ASCII.GetString(lineBytes.ToArray()).TrimEnd('\r', '\n');
        }

        private void WriteLine(Stream str, string line)
        {
            byte[] lineAsBytes = Encoding.ASCII.GetBytes(line + "\r\n");
            str.Write(lineAsBytes, 0, lineAsBytes.Length);
        }
    }
}

