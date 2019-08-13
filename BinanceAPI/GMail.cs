using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Auth.OAuth2;
using System.Threading;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using System.IO;

namespace BinanceAPI
{
    class GMail
    {
        static string[] Scopes = { GmailService.Scope.GmailSend };
        static string ApplicationName = "BinanceAPI";

        public static void SendIt(string message, string emailSender)
        {
            var msg = new AE.Net.Mail.MailMessage
            {
                Subject = "BinanceAPI Message",
                Body = message,
                From = new MailAddress(emailSender)
            };
            msg.To.Add(new MailAddress("email1@gmail.com"));
            msg.To.Add(new MailAddress("email2@gmail.com"));
            msg.ReplyTo.Add(msg.From); // Bounces without this!!
            var msgStr = new StringWriter();
            msg.Save(msgStr);

            var gmail = GService();
            
            var result = gmail.Users.Messages.Send(new Message
            {
                Raw = Base64UrlEncode(msgStr.ToString())
            }, "me").Execute();
            Console.WriteLine("Message ID {0} sent.", result.Id);
        }

        private static string Base64UrlEncode(string input)
        {
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            // Special "url-safe" base64 encode.
            return Convert.ToBase64String(inputBytes)
              .Replace('+', '-')
              .Replace('/', '_')
              .Replace("=", "");
        }

        private static GmailService GService()
        {
            UserCredential credential;

            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                                 System.Environment.SpecialFolder.Personal);

                credPath = Path.Combine(credPath, ".credentials/gmail-dotnet-quickstart.json");
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;

                var service = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });

                return service;
            }
        }
    }
}
