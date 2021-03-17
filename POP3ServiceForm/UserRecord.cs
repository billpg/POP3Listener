/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pop3ServiceForm
{
    internal sealed class UserRecord
    {
        internal readonly string User;
        internal readonly string Pass;
        internal readonly Dictionary<string,MessageRecord> Messages;

        public UserRecord(string user, string pass)
        {
            this.User = user;
            this.Pass = pass;
            this.Messages = new Dictionary<string, MessageRecord>();
        }

        public override string ToString() => User;
    }

    internal sealed class MessageRecord
    {
        internal readonly string uniqueID;
        internal readonly string fromAddress;
        internal readonly string toAddress;
        internal readonly string subjectText;
        internal readonly string bodyText;

        public MessageRecord(string uniqueID, string fromAddress, string toAddress, string subjectText, string bodyText)
        {
            this.uniqueID = uniqueID;
            this.fromAddress = fromAddress;
            this.toAddress = toAddress;
            this.subjectText = subjectText;
            this.bodyText = bodyText;
        }

        internal string AsRfc822 => $"From: {fromAddress}\r\nTo: {toAddress}\r\nSubject: {subjectText}\r\nX-Unique-ID: {uniqueID}\r\n\r\n{bodyText}\r\n";
    }
}

