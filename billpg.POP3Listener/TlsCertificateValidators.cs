/* billpg industries POP3 Listener. */
/* Copyright, William Godfrey 2021. All Rights Reserved. */
/* https://billpg.com/POP3/ */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace billpg.pop3
{
    public static class TlsCertificateValidators
    {
        public static byte[] ThumbprintSHA256Bytes(this X509Certificate cert)
        {
            using (var sha256 = SHA256.Create())
                return sha256.ComputeHash(cert.GetRawCertData());
        }

        public static string ThumprintSSHA256Hex(this X509Certificate cert)
            => string.Concat(ThumbprintSHA256Bytes(cert).Select(by => by.ToString("X2")));

        public static string ThumbprintSHA256Base64(this X509Certificate cert)
            => Convert.ToBase64String(ThumbprintSHA256Bytes(cert));

        public static bool IsThumbprintMatchAny(this X509Certificate cert, params string[] thumbprints)
            => IsThumbprintMatchAny(cert, thumbprints.AsEnumerable());

        public static bool IsThumbprintMatchAny(this X509Certificate cert, IEnumerable<string> thumbprints)
            => thumbprints.Any(thumbprint => IsThumbprintMatch(cert, thumbprint));

        public static bool IsThumbprintMatch(this X509Certificate cert, string thumbprint)
        {
            /* If thumbprint conatins colons or ends with equals, remove them. */
            thumbprint = thumbprint.Trim().Replace(":", "").TrimEnd('=');

            /* Attempt the hex thumbprint, allowing for any case. */
            if (string.Equals(cert.ThumprintSSHA256Hex(), thumbprint, StringComparison.InvariantCultureIgnoreCase))
                return true;

            /* Attempt base64, no allowance for case this time. */
            if (cert.ThumbprintSHA256Base64().TrimEnd('=') == thumbprint)
                return true;

            /* Failed all tests. */
            return false;
        }

        public static RemoteCertificateValidationCallback And(this RemoteCertificateValidationCallback x, RemoteCertificateValidationCallback y)
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => x(sender, cert, chain, errors) && y(sender, cert, chain, errors);
        }

        public static RemoteCertificateValidationCallback Or(this RemoteCertificateValidationCallback x, RemoteCertificateValidationCallback y)
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => x(sender, cert, chain, errors) || y(sender, cert, chain, errors);            
        }

        public static RemoteCertificateValidationCallback All(params RemoteCertificateValidationCallback[] validators)
            => All(validators.AsEnumerable());

        public static RemoteCertificateValidationCallback All(IEnumerable<RemoteCertificateValidationCallback> validators)
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => validators.All(val => val(sender, cert, chain, errors));
        }

        public static RemoteCertificateValidationCallback Any(params RemoteCertificateValidationCallback[] validators)
            => Any(validators.AsEnumerable());

        public static RemoteCertificateValidationCallback Any(IEnumerable<RemoteCertificateValidationCallback> validators)
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => validators.Any(val => val(sender, cert, chain, errors));
        }

        public static RemoteCertificateValidationCallback AllowPinned(params string[] allowHash)
            => AllowPinned(allowHash.AsEnumerable());

        public static RemoteCertificateValidationCallback AllowPinned(IEnumerable<string> allowHash)
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => IsThumbprintMatchAny(cert, allowHash);            
        }

        public static RemoteCertificateValidationCallback AllowPerPolicy()
        {
            return Internal;
            bool Internal(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                => errors == SslPolicyErrors.None;
        }
    }
}

