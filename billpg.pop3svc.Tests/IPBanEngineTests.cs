/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace billpg.pop3svc.Tests
{
    [TestClass]
    public class IPBanEngineTests
    {
        [TestMethod] 
        public void IPBan_ThreeStrikes_IPv4() 
            => ThreeStrikesInternal(Enumerable.Repeat(IPAddress.Parse("1.2.3.4"), 999));

        [TestMethod]
        public void IPBan_ThreeStrikes_IPv6()
            => ThreeStrikesInternal(Enumerable.Range(58, 99).Select(i => IPAddress.Parse($"2021:2456:9866:abcd:ef01:4444:3457:00{i}")));


        void ThreeStrikesInternal(IEnumerable<IPAddress> ipToBan)
        {
            /* Start with a default-state engine. */
            var ban = new ThreeStrikesBanEngine();

            /* Start enumerable of IP addresses. */
            var iterIP = ipToBan.GetEnumerator();

            /* Repeated attempts. The first three should be allowed with the others banned. */
            foreach (int attemptCount in Enumerable.Range(0, 9))
            {
                /* Load an IP from the collectipn and ban it. */
                iterIP.MoveNext();
                IPAddress ip = iterIP.Current;
                ban.RegisterFailedAttempt(ip);

                /* After three attempts (0,1,2), this IP is banned. */
                Assert.AreEqual(attemptCount > 2, ban.IsBanned(ip));
            }
        }
    }
}
