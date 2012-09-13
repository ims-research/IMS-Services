using System;
using System.Net;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;
using System.Net;
using System.Net.Mail;

namespace MessageToEmail
{
    internal class Server
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static readonly ILog IMLog = LogManager.GetLogger("IMLogger");
        private static SIPApp _app;
        

        private const string fromPassword = "imsim2emailpassword";
        private const string subject = "Subject";
        private static MailAddress fromAddress = new MailAddress("imsim2email@gmail.com", "IM 2 Email Server");
        private static SmtpClient smtp = new SmtpClient
                                      {
                                          Host = "smtp.gmail.com",
                                          Port = 587,
                                          EnableSsl = true,
                                          DeliveryMethod = SmtpDeliveryMethod.Network,
                                          UseDefaultCredentials = false,
                                          Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                                      };

        public static SIPStack CreateStack(SIPApp app, string proxyIp = null, int proxyPort = -1)
        {
            SIPStack myStack = new SIPStack(app);
            if (proxyIp != null)
            {
                myStack.ProxyHost = proxyIp;
                myStack.ProxyPort = (proxyPort == -1) ? 5060 : proxyPort;
            }
            return myStack;
        }

        public static void SendEmail(string to, string body)
        {
            MailAddress toAddress = new MailAddress(to,to);
            /*
            MailAddress toAddress = new MailAddress("richard.spiers@gmail.com", "Richard Spiers");*/
            using (
                MailMessage message = new MailMessage(fromAddress, toAddress)
                                                  {
                                                      Subject = subject,
                                                      Body = body
                                                  }
            )
            {
                smtp.Send(message);
            }
        }

        public static TransportInfo CreateTransport(string listenIp, int listenPort)
        {
            return new TransportInfo(IPAddress.Parse(listenIp), listenPort, System.Net.Sockets.ProtocolType.Udp);
        }

        static void AppResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Response Received:" + e.Message);
            Message response = e.Message;
            string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();
            switch (requestType)
            {
                case "INVITE":
                case "REGISTER":
                case "BYE":
                default:
                    Log.Info("Response for Request Type " + requestType + " is unhandled ");
                    break;
            }
        }

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Request Received:" + e.Message);
            Message request = e.Message;
            switch (request.Method.ToUpper())
            {
                case "MESSAGE":
                    {
                        IMLog.Info(request.First("From") + " says " + request.Body);
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        if (!request.First("Content-Type").ToString().ToUpper().Equals("APPLICATION/IM-ISCOMPOSING+XML"))
                        {
                            SendEmail("richard.spiers@gmail.com",request.Body);
                        }
                        break;
                    }
                case "INVITE":
                case "ACK":
                case "BYE":
                case "CANCEL":
                case "OPTIONS":
                case "REFER":
                case "SUBSCRIBE":
                case "NOTIFY":
                case "PUBLISH":
                case "INFO":
                default:
                    {
                        Log.Info("Request with method " + request.Method.ToUpper() + " is unhandled");
                        break;
                    }
            }
        }

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 7171);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI("im2email@open-ims.test");
            SendEmail("richard.spiers@gmail.com","im2email started");
            Console.ReadKey();
        }
    }
}
