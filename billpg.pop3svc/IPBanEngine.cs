/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace billpg.pop3svc
{
    /// <summary>
    /// Generic interface for a ban-by-IP address engine.
    /// </summary>
    public interface IIPBanEngine
    {
        /// <summary>
        /// Called when a client IP fails a login attempt.
        /// </summary>
        /// <param name="ip">P address of client.</param>
        void RegisterFailedAttempt(IPAddress ip);

        /// <summary>
        /// Called to query if an IP is banned.
        /// </summary>
        /// <param name="ip">Client IP being queried.</param>
        /// <returns>True if IP is banned. False otherwise.</returns>
        bool IsBanned(IPAddress ip);
    }

    public class ThreeStrikesBanEngine : IIPBanEngine
    {
        [System.Diagnostics.DebuggerDisplay("{ip}, {FailedAttemptCount}, {UtcLastAttempt}")]
        private class Entry
        {
            private readonly IPAddress ip;
            internal int FailedAttemptCount;
            internal DateTime UtcLastAttempt;

            public Entry(IPAddress ip)
            {
                this.ip = ip;
                this.FailedAttemptCount = 0;
                this.UtcLastAttempt = DateTime.MinValue;
            }

            internal void Strike()
            {
                this.FailedAttemptCount += 1;
                this.UtcLastAttempt = DateTime.UtcNow;
            }

            internal bool HasExpired(int expirySeconds)
            {
                /* Allow the default last-faulure as zero strikes. */
                if (this.UtcLastAttempt == DateTime.MinValue)
                    return false;

                /* Otherwise, return flag if we have gone past the expiry. */
                return DateTime.UtcNow > UtcLastAttempt.AddSeconds(expirySeconds);
            }
        }

        private readonly object mutex;
        private readonly Dictionary<IPAddress, Entry> banned;
        private int failedAttemptThreshold;
        private int attemptExpirySeconds;
        private int ipv6UserBits;

        public ThreeStrikesBanEngine()
        {
            this.mutex = new object();
            this.banned = new Dictionary<IPAddress, Entry>();
            this.failedAttemptThreshold = 3;
            this.attemptExpirySeconds = 10000;
            this.ipv6UserBits = 64;
        }

        public int FailedAttemptThreshold
        {
            get { return this.failedAttemptThreshold; }
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Threshold must be one or more.");
                this.failedAttemptThreshold = value;
            }
        }

        public int AttemptExpirySeconds
        {
            get { return attemptExpirySeconds; }
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Expiry must be one second or more.");
                this.attemptExpirySeconds = value;
            }
        }

        public int IPv6UserBits
        {
            get { return this.ipv6UserBits; }
            set
            {
                if (value < 1 || value > 128)
                    throw new ArgumentException("IPv6UserBits must be 1 to 128.");

                if (value % 8 != 0)
                    throw new ArgumentException("IPv6UserBits must be a multiple of 8. (For now.)");

                this.ipv6UserBits = value;
            }
        }

        private void NormalizeIP(ref IPAddress ip)
        {
            /* Do we need to normalize IPv6 Addresses? */
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && ipv6UserBits < 128)
            {
                /* Copy IP address to bytes. */
                byte[] ipBytes = ip.GetAddressBytes();

                /* Loop from the user byte boundary. */
                int startIndex = IPv6UserBits / 8;
                int maxIndex = 128 / 8;
                for (int addrByteIndex = startIndex; addrByteIndex < maxIndex; addrByteIndex += 1)
                    ipBytes[addrByteIndex] = 0;

                /* Normalize the last byte to a 1 */
                ipBytes[15] = 1;

                /* Convert back to an IP. */
                ip = new IPAddress(ipBytes);
            }
        }

        public void RegisterFailedAttempt(IPAddress ip)
        {
            /* Normalize IPv6 addresses. */
            NormalizeIP(ref ip);

            /* Prevent colliding updates. */
            lock (this.mutex)
            {
                /* Create record if needed. */
                if (this.banned.TryGetValue(ip, out var entry) == false)
                {
                    entry = new Entry(ip);
                    this.banned[ip] = entry;
                }
                
                /* Add a new strike to this IP. */
                entry.Strike();
            }
        }

        public bool IsBanned(IPAddress ip)
        {
            /* Normalize IPv6 addresses. */
            NormalizeIP(ref ip);

            /* Prevent colliding updates. */
            lock (this.mutex)
            {
                /* Is this one the list? */
                if (this.banned.TryGetValue(ip, out var entry))
                {
                    /* If entry has expired, handle. */
                    if (entry.HasExpired(this.attemptExpirySeconds))
                    {
                        /* Remove expired entry. */
                        this.banned.Remove(ip);

                        /* No longer banned. */
                        return false;
                    }

                    /* Return flag if attempt count is over the threshold. */
                    return entry.FailedAttemptCount >= this.failedAttemptThreshold;
                }

                /* Not on this list. */
                else
                    return false;
            }
        }
    }
}
