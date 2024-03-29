/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using billpg.pop3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pop3ServiceForm
{
    public partial class ServiceLauncher : Form
    {
        public ServiceLauncher()
        {
            InitializeComponent();
        }

        POP3Listener pop3;
        UserRecord SelectedUser => (UserRecord)usersList.SelectedItem;
        IEnumerable<UserRecord> AllUsers => usersList.Items.Cast<UserRecord>();

        private void ServiceLauncher_Load(object sender, EventArgs e)
        {
            UserRecord meUser = new UserRecord("me", "passw0rd");            
            string uid = $"{Guid.NewGuid()}".ToUpperInvariant();
            meUser.Messages.Add(uid, new MessageRecord(uid, "me@example.com", "you@example.com", "Welcome to billpg industries POP3 Service", "Rutabaga"));

            usersList.Items.Clear();
            usersList.Items.Add(meUser);
            usersList.SelectedIndex = 0;
            RefreshMessagesView();


            var cert = new X509Certificate2("SelfSigned.pfx", "Rutabaga");

            pop3 = new POP3Listener();
            pop3.ListenOnStandard(IPAddress.Loopback);
            pop3.RequireSecureLogin = true;
            pop3.SecureCertificate = cert;
            pop3.ServiceName = "Pop3ServiceForm";
            pop3.Events.OnAuthenticate = OnAuthenticateHandler;
            pop3.Events.OnMessageList = OnMessageListHandler;
            pop3.Events.OnMessageRetrieval = OnMessageRetrievalHandler;
            pop3.Events.OnMessageDelete = OnMessageDeleteHandler;
        }

        private void AddLogEntry(string entry)
        {
            this.Invoke(new Action(Internal));
            void Internal()
            {
                listBox1.Items.Add(entry);
                listBox1.TopIndex = listBox1.Items.Count - 1;
            }
        }

        static void DoNothing()
        { }

        void OnAuthenticateHandler(POP3AuthenticationRequest req)
        {
            req.AuthMailboxID = (string)this.Invoke(new Func<string>(Internal));
            string Internal()
            {
                foreach (var userAvail in AllUsers)
                {
                    if (userAvail.User == req.SuppliedUsername && userAvail.Pass == req.SuppliedPassword)
                        return req.SuppliedUsername;
                }

                return null;
            }
        }

        IEnumerable<string> OnMessageListHandler(string mailboxID)
        {
            IList<string> uniqueIDs = null;
            this.Invoke(new Action(Internal));
            return uniqueIDs;

            void Internal()
            {
                uniqueIDs = UserByName(mailboxID).Messages.Keys.ToList();
            }
        }

        bool ShowMessageDeleteForm(string user, IList<string> messages)
        {
            var formResponse = MessageBox.Show(this, $"Delete {string.Join(",", messages)} ?", "Delete", MessageBoxButtons.YesNo);
            if (formResponse == DialogResult.Yes)
            {
                var allMessages = Program.MessageList(user);
                foreach (var messageToDelete in messages)
                {
                    allMessages.Remove(messageToDelete);
                }
                return true;
            }

            else
                return false;
        }

        private UserRecord UserByName(string user) => AllUsers.Where(u => u.User == user).Single();


        private void OnMessageRetrievalHandler(POP3MessageRetrievalRequest request)
        {
            Invoke(new Action(Internal));
            void Internal()
            {
                string rfc = UserByName(request.AuthMailboxID).Messages[request.MessageUniqueID].AsRfc822;
                var lines = rfc.Split('\r').Select(line => line.Trim()).ToList();
                request.UseEnumerableLines(lines);
            }
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


        void OnMessageDeleteHandler(string mailboxID, IList<string> uniqueIDs)
        {
            Invoke(new Action(Internal));
            void Internal()
            {
                var user = UserByName(mailboxID);
                foreach (var msgToRemove in uniqueIDs)
                    user.Messages.Remove(msgToRemove);

                RefreshMessagesView();
            }
        }

        private void addUserNameTextBox_TextChanged(object sender, EventArgs e)
        {
            bool isOkay = addUserNameTextBox.Text.Length > 0 && addUserPassTextBox.Text.Length > 0;
            addUserCmd.Enabled = isOkay;
        }

        private void usersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshMessagesView();
        }

        private void RefreshMessagesView()
        {
            messageList.Rows.Clear();
            if (SelectedUser == null)
                return;

            foreach (var message in SelectedUser.Messages.Values)
            {
                messageList.Rows.Add(message.uniqueID, message.fromAddress, message.toAddress, message.subjectText, message.bodyText);
            }
        }

        private void addMessageCmd_Click(object sender, EventArgs e)
        {
            var frm = new MessageContentsForm();
            frm.UniqueID = $"{Guid.NewGuid()}".ToUpperInvariant();
            var result = frm.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                SelectedUser.Messages[frm.UniqueID] = frm.AsMessageRecord;
                RefreshMessagesView();
            }
        }

        private void addUserCmd_Click(object sender, EventArgs e)
        {
            usersList.Items.Add(new UserRecord(addUserNameTextBox.Text, addUserPassTextBox.Text));
            usersList.SelectedIndex = usersList.Items.Count - 1;
            RefreshMessagesView();
            addUserNameTextBox.Text = "";
            addUserPassTextBox.Text = "";
        }
    }
}

