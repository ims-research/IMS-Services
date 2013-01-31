using System;
using System.Collections.Generic;
using System.Net;
using System.Timers;
using System.Xml;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;
using System.Net.Mail;
using Timer = System.Timers.Timer;

namespace MessageToEmail
{
    static class Server
    {
        private static readonly ILog ConsoleLog = LogManager.GetLogger("ConsoleLog");
        private static readonly ILog SIPLog = LogManager.GetLogger("SIPLog");
        private static readonly ILog DebugLog = LogManager.GetLogger("DebugLog");
        private static readonly ILog ServiceLog = LogManager.GetLogger("ServiceLog");
        private static string _localIP;
        private const int LocalPort = 7171;
        private static SIPApp _app;
        private static Address _localparty;
        

        private const string FromPassword = "imsim2emailpassword";
        private const string Subject = "Subject";
        private static MailAddress fromAddress = new MailAddress("imsim2email@gmail.com", "IM 2 Email Server");
        private static SmtpClient smtp = new SmtpClient
                                      {
                                          Host = "smtp.gmail.com",
                                          Port = 587,
                                          EnableSsl = true,
                                          DeliveryMethod = SmtpDeliveryMethod.Network,
                                          UseDefaultCredentials = false,
                                          Credentials = new NetworkCredential(fromAddress.Address, FromPassword)
                                      };

        private static SIPStack CreateStack(SIPApp app, string proxyIp = null, int proxyPort = -1)
        {
            SIPStack myStack = new SIPStack(app);
            if (proxyIp != null)
            {
                myStack.ProxyHost = proxyIp;
                myStack.ProxyPort = (proxyPort == -1) ? 5060 : proxyPort;
            }
            return myStack;
        }

        private static void SendEmail(string to, string body)
        {
            MailAddress toAddress = new MailAddress(to,to);
            using (
                MailMessage message = new MailMessage(fromAddress, toAddress)
                                                  {
                                                      Subject = Subject,
                                                      Body = body
                                                  }
            )
            {
                smtp.Send(message);
            }
        }

        private static void GetMetrics(Object sender, ElapsedEventArgs e)
        {
            Dictionary<string, float> metrics = Metrics.GetResourceUsage();
            UpdateServiceMetrics(metrics);
        }

        private static void StartTimer()
        {
            Timer aTimer = new Timer();
            aTimer.Elapsed += GetMetrics;
            aTimer.Interval = 15000;
            aTimer.Enabled = true;
        }

        private static void UpdateServiceMetrics(Dictionary<string, float> metrics)
        {
            UserAgent pua = new UserAgent(_app.Stack)
            {
                RemoteParty = new Address("<sip:voicemail@open-ims.test>"),
                LocalParty = _localparty
            };
            Message request = pua.CreateRequest("PUBLISH");
            request.InsertHeader(new Header("service-description", "Event"));
            request.InsertHeader(new Header("application/SERV_DESC+xml", "Content-Type"));
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("Resources/ServiceDescription.xml");
            XmlNode node = xmlDoc.SelectSingleNode("Service/Metrics/TotalCPU");
            node.InnerText = String.Format("{0:0.##}", metrics["totalCPU"]);

            node = xmlDoc.SelectSingleNode("Service/Metrics/CPU");
            node.InnerText = String.Format("{0:0.##}", metrics["cpu"]);

            node = xmlDoc.SelectSingleNode("Service/Metrics/TotalMemory");
            node.InnerText = String.Format("{0:0.##}", metrics["memAvailable"]) + " MB";

            node = xmlDoc.SelectSingleNode("Service/Metrics/Memory");
            node.InnerText = String.Format("{0:0.##}", ((metrics["memUsed"] / 1024) / 1024)) + " MB";

            request.Body = xmlDoc.OuterXml;
            pua.SendRequest(request);
        }


        private static TransportInfo CreateTransport(string listenIp, int listenPort)
        {
            return new TransportInfo(IPAddress.Parse(listenIp), listenPort, System.Net.Sockets.ProtocolType.Udp);
        }

        static void AppResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            Message response = e.Message;
            string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();

            SIPLog.Info(response);
            ConsoleLog.Info(response.ResponseCode + " response received for "+requestType+" request");

            switch (requestType)
            {
                case "PUBLISH":
                    {
                        if (response.ResponseCode == 200)
                        {
                            DebugLog.Info("Successfully sent service information to SRS");
                        }
                        else
                        {
                            ConsoleLog.Warn("Received non 200 OK response"); 
                            ConsoleLog.Warn(response);
                        }
                        break;
                    }
                default:
                    ConsoleLog.Warn("Response for Request Type " + requestType + " is unhandled ");
                    break;
            }
        }

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Message request = e.Message;

            SIPLog.Info(request);
            ConsoleLog.Info(request.Method.ToUpper() + " request received");

            switch (request.Method.ToUpper())
            {
                case "MESSAGE":
                    {
                        ServiceLog.Info(request.First("From") + " says " + request.Body);
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        if (!request.First("Content-Type").ToString().ToUpper().Equals("APPLICATION/IM-ISCOMPOSING+XML"))
                        {
                            SendEmail("richard.spiers@gmail.com",request.Body);
                            ServiceLog.Info("Sent email to richard.spiers@gmail.com");
                        }
                        break;
                    }
                default:
                    {
                        DebugLog.Info("Request with method " + request.Method.ToUpper() + " is unhandled");
                        Message m = e.UA.CreateResponse(501, "Not Implemented");
                        e.UA.SendResponse(m);
                        break;
                    }
            }
        }

        private static void PublishService(bool determineIP, int port)
        {
            UserAgent pua = new UserAgent(_app.Stack) { RemoteParty = new Address("<sip:ims2email@open-ims.test>"), LocalParty = _localparty };
            Message request = pua.CreateRequest("PUBLISH");
            request.InsertHeader(new Header("service-description", "Event"));
            request.InsertHeader(new Header("application/SERV_DESC+xml", "Content-Type"));
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("Resources/ServiceDescription.xml");
            if (determineIP)
            {
                XmlNode IPnode = xmlDoc.SelectSingleNode("Service/Service_Config/Server_IP");
                IPnode.InnerText = _localIP;
            }
            XmlNode Portnode = xmlDoc.SelectSingleNode("Service/Service_Config/Server_Port");
            Portnode.InnerText = Convert.ToString(port);
            xmlDoc.Save("Resources/ServiceDescription.xml");
            request.Body = xmlDoc.OuterXml;
            pua.SendRequest(request);
            ConsoleLog.Info("Sent service information to SRS");
        }

        static void Main()
        {
            if (String.IsNullOrEmpty(_localIP))
            {
                _localIP = Helpers.GetLocalIP();    
            }
            TransportInfo localTransport = CreateTransport(_localIP, LocalPort);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent +=     AppRequestRecvEvent;
            _app.ResponseRecvEvent += AppResponseRecvEvent;
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI("im2email@open-ims.test");
             _localparty = new Address("<sip:ims2email@open-ims.test>");
            PublishService(true,LocalPort);
            StartTimer();
            Console.ReadKey();
        }

    }
}
