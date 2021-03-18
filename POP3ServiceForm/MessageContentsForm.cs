/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pop3ServiceForm
{
    public partial class MessageContentsForm : Form
    {
        public MessageContentsForm()
        {
            InitializeComponent();
        }

        internal string UniqueID
        {
            get { return uniqueIdTextBox.Text; }
            set { uniqueIdTextBox.Text = value; }
        }

        internal string FromAddress
        {
            get { return fromTextBox.Text; }
            set { fromTextBox.Text = value; }
        }

        internal string ToAddress
        {
            get { return toTextBox.Text; }
            set { toTextBox.Text = value; }
        }

        internal string SubjectText
        {
            get { return subjectTextBox.Text; }
            set { subjectTextBox.Text = value; }
        }

        internal string BodyText
        {
            get { return contentsTextBox.Text; }
            set { contentsTextBox.Text = value; }
        }

        internal MessageRecord AsMessageRecord
            => new MessageRecord(UniqueID, FromAddress, ToAddress, SubjectText, BodyText);

        internal IEnumerable<string> MessageContents
            => contentsTextBox.Text.Split('\n').Select(line => line.Trim());

        private void submitCmd_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void MessageContentsForm_Load(object sender, EventArgs e)
        {

        }

        private void populateCmd_Click(object sender, EventArgs e)
        {
            ToAddress = RandomEmailAddress();
            FromAddress = RandomEmailAddress();
            SubjectText = "What's the best " + ("root vegetable/chocolate/christmas food".Split('/')[rnd.Next(3)]) + "?";
            BodyText = "I dunno.\r\n\r\nThis goes over a few lines though.";
        }

        private static Random rnd = new Random();

        private string RandomEmailAddress()
        {
            return "bill,deeny,ollie,danny,rob,josh,dan".Split(',')[rnd.Next(7)] + "@example.com";
        }
    }
}

