using System;
using System.Collections.Generic;
using System.Text;

namespace billpg.pop3
{
    public class POP3AuthenticationRequest
    {
        public IPOP3ConnectionInfo ConnectionInfo { get; }
        public string SuppliedUsername { get; }
        public string SuppliedPassword { get; }

        internal POP3AuthenticationRequest(IPOP3ConnectionInfo info, string suppliedUsername, string suppliedPassword)
        {
            this.ConnectionInfo = info;
            this.SuppliedUsername = suppliedUsername;
            this.SuppliedPassword = suppliedPassword;
        }

        public string AuthUserID { get; set; } = null;
        public IPOP3Mailbox MailboxProvider { get; set; } = null;
    }
}
