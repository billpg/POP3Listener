/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace PopAndCircumstance
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MainInternal(args);
            }
            catch (ConsoleWriteLineException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void MainInternal(string[] args)
        {
            /* Defaults for no-parameters. */
            var insecurePorts = new List<int>{110};
            var securePorts = new List<int>{995};

            /* Look for port args. */
            foreach (string arg in args)
            {
                /* Split between key and value. */
                string key, value;
                int equalIndex = arg.IndexOf('=');
                if (equalIndex < 0)
                {
                    key = arg;
                    value = "";
                }
                else
                {
                    key = arg.Substring(0, equalIndex);
                    value = arg.Substring(equalIndex+1);
                }

                /* Unsecured ports. */
                if (EqualsNoCase(key, "INSPORT"))
                    insecurePorts = ParsePortsCsv(value);                    

                /* Secure ports. */
                else if (EqualsNoCase(key, "SECPORT"))
                    securePorts = ParsePortsCsv(value);
            }

            /* Open a flag to raise as a stop instruction. */
            using (var stopEvent = new ManualResetEvent(false))
            {
                /* Announce start. */
                Console.WriteLine("Starting POP3 Service. Type QUIT or EXIT to stop.");

                /* Launch the service. */
                var prov = new RandomProvider(stopEvent, insecurePorts, securePorts);
                prov.Start();

                /* Loop, waiting for a console command. */
                while (true)
                {
                    string command = Console.ReadLine();
                    if (EqualsNoCaseAny(command, "EXIT", "QUIT"))
                        break;
                }

                /* Shut down provider. */
                stopEvent.Set();
            }
        }

        static bool EqualsNoCaseAny(string x, params string[] anyy)
            => anyy.Any(y => EqualsNoCase(x, y));
        
        static bool EqualsNoCase(string x, string y)
            => string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);

        private static List<int> ParsePortsCsv(string csv)
        {
            var resp = new SortedSet<int>();
            foreach (var portAsString in csv.Split(','))
            {
                /* Skip empty items. */
                if (string.IsNullOrWhiteSpace(portAsString))
                    continue;

                /* Attempt parse. */
                if (int.TryParse(portAsString.Trim(), out int port) == false || port < 1 || port > 65535)
                    throw new ConsoleWriteLineException("Ivalid port number: " + portAsString);

                /* Store in response. */
                resp.Add(port);
            }

            /* Completed collection. */
            return resp.ToList();
        }
    }

    internal class ConsoleWriteLineException : Exception
    {
        public ConsoleWriteLineException(string message) 
        : base(message)
        {
        }
    }
}

