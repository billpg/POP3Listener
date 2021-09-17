/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace PopAndCircumstance
{
    internal class RandomProvider
    {
        private readonly ManualResetEvent stopEvent;
        private readonly Thread worker;
        private readonly List<ProviderUser> users;
        private IEnumerable<string> allUserNames => users.Select(u => u.UserName);
        private readonly IList<int> insecurePorts;
        private readonly  IList<int> securePorts;

        public string Name => "PopAndCircumstance";

        static private readonly IList<string> availAddresses =
            new string[]
            {
                "bill@example.com", "rob@example.com", "ollie@example.com", "danny@example.com", "deeny@example.com",
                "scott@example.com", "marc@example.com", "simon@example.com", "greg@exmaple.com", "fraser@example.com"
            }.ToList().AsReadOnly();
        private static readonly Random rnd = new Random();

        private readonly static object logMutex = new object();
        private static void LogWrite(string text)
        {
            lock (logMutex)
                Console.WriteLine(text);
        }

        internal RandomProvider(ManualResetEvent stopEvent, IList<int> insecurePorts, IList<int> securePorts)
        {
            this.stopEvent = stopEvent;
            this.worker = new Thread(ThreadMain);
            this.users = new List<ProviderUser>();
            this.insecurePorts = insecurePorts;
            this.securePorts = securePorts;
        }

        internal void Start()
        {
            this.worker.Start();
        }

        private void ThreadMain()
        {
            /* Launch the listener. */
            var listen = new billpg.pop3.POP3Listener();
            listen.OnAuthenticate = this.OnAuthenticateHandler;
            foreach (int port in this.insecurePorts)
                listen.ListenOn(IPAddress.Loopback, port, false);
            foreach (int port in this.securePorts)
                listen.ListenOn(IPAddress.Loopback, port, true);

            /* Keep looping until stop signal. */
            bool firstTime = true;
            while (true)
            {
                /* Check the stop signal. */
                if (firstTime == false)
                {
                    bool stopNow = stopEvent.WaitOne(10000);
                    if (stopNow)
                        break;                    
                }
                firstTime = false;

                /* Create a user? */
                if (users.Count == 0 || (users.Count < 10 && rnd.Next(20) == 0))
                {
                    /* Create a user. */
                    string popUserName = RandomEmail(allUserNames);
                    string popPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=');
                    ProviderUser newUser = new ProviderUser(popUserName, popPassword);
                    users.Add(newUser);

                    /* Announce user to caller. */
                    LogWrite($"***** New User. Use command \"XLOG {popUserName} {popPassword}\" to login.");
                }

                /* Create an email message, */
                var user = users[rnd.Next(users.Count)];
                if (user.MessageCount > 10000)
                {
                    LogWrite($"***** User {user.UserName} has reached maximum quota of messages");
                }
                else
                {
                    string uid = $"{Guid.NewGuid()}";
                    user.AddMessage(uid);
                    LogWrite($"***** User {user.UserName} has a new message. Count={user.MessageCount}.");
                }

            }

            /* Stop the service. */
            listen.Stop();
        }



        internal static string RandomEmail(IEnumerable<string> except)
        {
            var choices = availAddresses.Except(except ?? new List<string>()).ToList();
            return choices[rnd.Next(choices.Count)];
        }
        internal static string RandomEmail(params string[] except)
            => RandomEmail(except.AsEnumerable());

        internal void OnAuthenticateHandler(POP3AuthenticationRequest req)
        {
            /* Look for the claimed user record. */
            ProviderUser user = users.SingleOrDefault(u => u.UserName == req.SuppliedUsername);
            if (user == null)
            {
                LogWrite($"***** No such user {req.SuppliedUsername} for login attempt by {req.ConnectionInfo.ClientIP}");
                req.AllowRequest = false;
                return;
            }

            /* Does the password match? */
            if (user.PassWord == req.SuppliedPassword)
            {
                LogWrite($"***** Successful login by {req.SuppliedUsername} by {req.ConnectionInfo.ClientIP}");
                req.AllowRequest = true;
                req.MailboxProvider = user;
                return;
            }

            /* Wrong password. */
            LogWrite($"***** Wrong password attempt for {req.SuppliedUsername} by {req.ConnectionInfo.ClientIP}");
            req.AllowRequest = false;
        }

        public void RegisterNewMessageAction(RaiseNewMessageEvent onNewMessage)
        {
            /* Nothing to do. */
        }

        private class ProviderUser : IPOP3Mailbox
        {
            internal readonly string UserName;
            internal readonly string PassWord;
            private readonly List<RandomMessage> messages;

            public ProviderUser(string userName, string password)
            {
                this.UserName = userName;
                this.PassWord = password;
                this.messages = new List<RandomMessage>();
            }

            internal IEnumerable<string> UniqueIDs => messages.Select(m => m.UniqueID);
            internal int MessageCount => messages.Count;
            bool IPOP3Mailbox.MailboxIsReadOnly(IPOP3ConnectionInfo info) => false;

            internal void AddMessage(string uid)
            {
                messages.Add(new RandomMessage(UserName, uid));
            }

            public string UserID(IPOP3ConnectionInfo info) => UserName;

            public IList<string> ListMessageUniqueIDs(IPOP3ConnectionInfo info)
                => UniqueIDs.ToList().AsReadOnly();

            public bool MessageExists(IPOP3ConnectionInfo info, string uniqueID)
                => UniqueIDs.Contains(uniqueID);

            public long MessageSize(IPOP3ConnectionInfo into, string uniqueID)
            {
                /* Hello everyone. I'm curious to learn if the reported 
                 * size of the message in a POP3 mailbox actually matters.
                 * If using a random number here has actually broken something
                 * with your POP3 client, please raise an issue on this 
                 * project's guithub. */

                /* Return a size with the selected random magnitude. */
                return RandomMagnitude(0, rnd.Next(2, 10));

                long RandomMagnitude(long prefix, int digits)
                {
                    if (digits == 0)
                        return prefix;
                    else
                        return RandomMagnitude(prefix * 10 + rnd.Next(0,10), digits-1);
                }
            }



            public IMessageContent MessageContents(IPOP3ConnectionInfo info, string uniqueID)
                => messages.SingleOrDefault(m => m.UniqueID == uniqueID);

            public void MessageDelete(IPOP3ConnectionInfo info, IList<string> uniqueIDs)
            {
                foreach (var message in messages.ToList())
                {
                    if (uniqueIDs.Contains(message.UniqueID))
                    {
                        messages.Remove(message);
                        LogWrite($"***** Deleted message {message.UniqueID}.");
                    }
                }
            }

            private class RandomMessage : IMessageContent
            {
                internal readonly string UniqueID;
                private readonly List<string> lines;
                private int currentLineIndex;
                private static int counter = 0;

                private static readonly IList<string> subjects 
                    = new string[]
                    {
                        "He's amazing! He's fantastic! He's the greatest secret agent in the world.",
                        "Here's a llama, there's a llama, and another little llama.",
                        "We choose to send this message, not because it is easy but because it is hard.",
                        "TV says donuts are high in fat, Kazoo.",
                        "I shall use my holy powers to defeat the British.",
                        "Nothing is enough for the man to whom enough is too little."
                    }.ToList().AsReadOnly();

                public RandomMessage(string recip, string uniqueID)
                {
                    this.UniqueID = uniqueID;
                    lines = new List<string>()                    
                    {
                        $"From: {RandomEmail(recip)}",
                        $"To: {recip}",
                        $"Subject: {subjects[rnd.Next(subjects.Count)]}",
                        $"X-UniqueID: {uniqueID}",
                        "",
                        "Hello.",
                        $"This is message #{Interlocked.Increment(ref counter)} sent at {DateTime.Now.ToShortTimeString()}.",
                        "I hope you like it."
                    };
                    this.currentLineIndex = 0;
                }

                public void Close()
                {
                    this.currentLineIndex = 0;
                }

                public string NextLine()
                {
                    if (currentLineIndex == lines.Count)
                    {
                        currentLineIndex = 0;
                        return null;
                    }
                    else
                    {
                        return lines[currentLineIndex++];
                    }
                }
            }
        }
    }
}
