using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.pop3.Tests
{
    [TestClass]
    public class ManyConnectionsUnitTest
    {
        private int port;
        private readonly Random rnd = new Random();
        private readonly List<string> errors = new List<string>();

        void AssertAreEqual(string expected, string actual, [CallerLineNumber] int lineNumber = 0)
        {
            if (expected != actual)
                lock (errors)
                    errors.Add($"AssertAreEqual failure at line {lineNumber}: Expected:{expected} Actual:{actual}");
        }

        void AssertIsTrue(bool cond, string message, [CallerLineNumber] int lineNumber = 0)
        {
            if (cond == false)
                lock (errors)
                    errors.Add($"AssertIsTrue failure line {lineNumber}: {message}");
        }

        private void AssertEqualAny(string[] expected, string actual, [CallerLineNumber] int lineNumber = 0)
        {
            if (expected.Contains(actual) == false)
                lock (errors)
                    errors.Add($"AssertEqualAny failure at line {lineNumber}: Expected:{expected} Actual:{actual}");
        }


        private HashSet<string> ClaimedUniqueIDs = new HashSet<string>();
        bool Claim(string req)
        {
            lock (ClaimedUniqueIDs)
            {
                if (ClaimedUniqueIDs.Contains(req))
                    return false;
                ClaimedUniqueIDs.Add(req);
                return true;
            }
        }

        void Release(string rel)
        {
            lock (ClaimedUniqueIDs)
                ClaimedUniqueIDs.Remove(rel);
        }

        List<string> mailboxUniqueIDs = new List<string>();
        int nextUniqueID = 1;
        void AddToMailbox() => mailboxUniqueIDs.Add($"UnitTest_{nextUniqueID++}_{GenRandomString()}");

        [TestMethod]
        public void POP3_ManyConnections()
        {
            /* Start mailbox with five hundred uniqueIDs. */
            for (int i = 0; i < 500; i++) AddToMailbox();

            /* Set up a listener. */
            var pop3 = new POP3Listener();
            pop3.Events.OnAuthenticate = req => req.AuthMailboxID = "x";

            /* The mailbox list will return a random half the available messages. */
            pop3.Events.OnMessageList = MyListMessages;
            IEnumerable<string> MyListMessages(string mailboxID)
            {
                lock (mailboxUniqueIDs)
                    return mailboxUniqueIDs.Shuffle().Take(mailboxUniqueIDs.Count / 10 + 1).ToList();
            }

            /* Delete messages on request. */
            pop3.Events.OnMessageDelete = MyDelete;
            void MyDelete(string mailboxID, IList<string> toDels)
            {
                lock (mailboxUniqueIDs)
                {
                    foreach (string del in toDels)
                    {
                        AssertIsTrue(mailboxUniqueIDs.Contains(del), "Attempt to delete not-existant message.");
                        mailboxUniqueIDs.Remove(del);
                    }
                }
            }

            List<string> commands = new List<string>();
            pop3.Events.OnCommandReceived = MyLogCommand;           
            void MyLogCommand(IPOP3ConnectionInfo info, string command)
            {
                lock (commands)
                    commands.Add(command);
            }

            /* Start listening. */
            port = rnd.Next(1030, 65530);
            pop3.ListenOn(IPAddress.Loopback, port, false);

            /* Launch a hundred client threads. */
            var threads = Enumerable.Range(0, 100).Select(LaunchClient).ToList();
            Thread LaunchClient(int x)
            {
                Thread th = new Thread(ClientMain);
                th.Start();
                return th;
            }

            /* Wait for the mailbox to be exhausted. */
            while (true)
            {
                /* Check the mailbox. */
                lock (mailboxUniqueIDs)
                {
                    /* If empty, stop now. */
                    if (mailboxUniqueIDs.Any() == false)
                        break;
                }

                /* Check for errors. */
                lock (errors)
                {
                    if (errors.Any())
                        Assert.Fail(errors[0]);
                }

                /* Wait a decisecond between checks. */
                Thread.Sleep(100);
            }

            /* Wait for all the client threads to end. */
            foreach (var thWaiting in threads)
                thWaiting.Join();

            /* Check for errors. */
            lock (errors)
            {
                if (errors.Any())
                    Assert.Fail(errors[0]);
            }
        }

        private string GenRandomString()
        {
            byte[] token = new byte[12];
            rnd.NextBytes(token);
            return Convert.ToBase64String(token);
        }

        void ClientMain()
        { 
            /* Open new connection. */
            using var tcp = new TcpClient("localhost", port);
            using var str = tcp.GetStream();

            /* Wait for banner. */
            var banner = str.ReadLine();
            AssertAreEqual("+OK POP3 service by billpg industries https://billpg.com/POP3/", banner);

            /* Login. */
            str.WriteLine("XLOG x x");
            var loginResp = str.ReadLine();
            AssertAreEqual("+OK Welcome.", loginResp);

            /* Keep looping until done. */
            while (true)
            {
                /* Load UIDL */
                str.WriteLine("UIDL");
                var uidlResp = str.ReadLine();
                AssertAreEqual("+OK Unique-IDs follow...", uidlResp);
                var uidl = new Dictionary<int, string>();
                while (true)
                {
                    var uidlItem = str.ReadLine();
                    if (uidlItem == ".")
                        break;

                    int messageid = int.Parse(uidlItem.Split(' ')[0]);
                    string uniqueid = uidlItem.Split(' ')[1];
                    uidl.Add(messageid, uniqueid);
                }

                /* If UIDL empty, stop. */
                if (uidl.Count == 0)
                {
                    str.WriteLine("QUIT");
                    var quitResp = str.ReadLine();
                    AssertAreEqual("+OK Closing connection.", quitResp);
                    break;
                }

                /* Loop through messages and claim. */
                int deletedThisSession = 0;
                foreach (var uid in uidl)
                {
                    if (Claim(uid.Value))
                    {
                        str.WriteLine($"DELE {uid.Key}");
                        var deleResp = str.ReadLine();
                        AssertAreEqual($"+OK Message UID:{uid.Value} flagged for delete on QUIT or SLEE.", deleResp);
                        deletedThisSession++;
                    }

                    if (deletedThisSession > 10)
                        break;
                }

                /* Go to sleep to commit deletes. */
                str.WriteLine("SLEE");
                var sleeResp = str.ReadLine();
                AssertAreEqual($"+OK Zzzzz. Deleted {deletedThisSession} messages.", sleeResp);
                Thread.Sleep(1000);

                /* Wake up to refresh mailbox. */
                str.WriteLine("WAKE");
                var wakeResp = str.ReadLine();
                AssertEqualAny(new string[] { "+OK [ACTIVITY/NEW] Welcome back.", "+OK [ACTIVITY/NONE] Welcome back." }, wakeResp);
            }
        }

    }
}
