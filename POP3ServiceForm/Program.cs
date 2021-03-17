/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pop3ServiceForm
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ServiceLauncher());
        }

        private static readonly Dictionary<string, List<string>> allMessages 
            = new Dictionary<string, List<string>>();

        internal static List<string> MessageList(string user)
        {
            if (allMessages.TryGetValue(user, out List<string> messages))
                return messages;

            var userMessages = new List<string>();
            allMessages.Add(user, userMessages);
            return userMessages;
        }
    }
}

