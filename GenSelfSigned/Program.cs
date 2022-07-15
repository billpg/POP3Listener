using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace TestTlsService
{
    class Program
    {
        static void Main(string[] args)
        {
            /* Create a cert. */
            var cr = new CertificateRequest(new X500DistinguishedName("cn=this.is.invalid"), RSA.Create(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using (var cert = cr.CreateSelfSigned(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddYears(+1)))
            {
                var exp = cert.Export(X509ContentType.Pfx);
                var tempCert = Path.Combine(@"C:\Users\hacke\source\repos\billpg\POP3Listener\POP3ServiceForm", "SelfSigned.pfx");
                File.WriteAllBytes(tempCert, exp);
            }
        }
    }
}