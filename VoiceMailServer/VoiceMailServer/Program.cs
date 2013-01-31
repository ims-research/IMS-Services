using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Xml;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;
using Timer = System.Timers.Timer;

namespace VoiceMailServer
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof (SIPApp));
        private static readonly ILog SessionLog = LogManager.GetLogger("SessionLogger");
        private static SIPApp _app;
        private static Address _localparty;
        private static string _localIP;
        private const int LocalPort = 7000;

       

        private static void GetMetrics(Object sender, ElapsedEventArgs e)
        {
            Dictionary<string,float> metrics = Metrics.GetResourceUsage();
            UpdateServiceMetrics(metrics);
        }

        private static void StartTimer()
        {
            Timer aTimer = new Timer();
            aTimer.Elapsed += GetMetrics;
            aTimer.Interval = 15000;
            aTimer.Enabled = true;
        }

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

        public static TransportInfo CreateTransport(string listenIp, int listenPort)
        {
            return new TransportInfo(IPAddress.Parse(listenIp), listenPort, ProtocolType.Udp);
        }

        private static void AppResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Response Received:" + e.Message);
            Message response = e.Message;
            string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();
            switch (requestType)
            {
                case "PUBLISH":
                    {
                        if (response.ResponseCode == 200)
                        {
                            Log.Info("Successfully sent service information to SRS");
                        }
                        break;
                    }
                default:
                    Log.Info("Response for Request Type " + requestType + " is unhandled ");
                    break;
            }
        }

        private static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Request Received:" + e.Message);
            Message request = e.Message;
            switch (request.Method.ToUpper())
            {
                case "INVITE":
                    {
                        SessionLog.Info("Call received from " + request.First("From"));
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                case "BYE":
                    {
                        SessionLog.Info("Call ended from    " + request.First("From"));
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                case "CANCEL":
                    {
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                default:
                    {
                        Log.Info("Request with method " + request.Method.ToUpper() + " is unhandled");
                        Message m = e.UA.CreateResponse(501, "Not Implemented");
                        e.UA.SendResponse(m);
                        break;
                    }
            }
        }

        private static void UpdateServiceMetrics(Dictionary<string,float> metrics )
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
            node.InnerText = String.Format("{0:0.##}",metrics["totalCPU"]);

            node = xmlDoc.SelectSingleNode("Service/Metrics/CPU");
            node.InnerText = String.Format("{0:0.##}", metrics["cpu"]);

            node = xmlDoc.SelectSingleNode("Service/Metrics/TotalMemory");
            node.InnerText = String.Format("{0:0.##}", metrics["memAvailable"]) + " MB";

            node = xmlDoc.SelectSingleNode("Service/Metrics/Memory");
            node.InnerText = String.Format("{0:0.##}", ((metrics["memUsed"] / 1024) / 1024))+ " MB";

            request.Body = xmlDoc.OuterXml;
            pua.SendRequest(request);
        }

        private static void PublishService(bool determineIP, int port)
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
            if (determineIP)
            {
                XmlNode ipNode = xmlDoc.SelectSingleNode("Service/Service_Config/Server_IP");
                if (ipNode == null)
                {
                    Log.Error("Service XML does not contain Server IP node");
                    return;
                }
                ipNode.InnerText = _localIP;
            }
            XmlNode portNode = xmlDoc.SelectSingleNode("Service/Service_Config/Server_Port");
            if (portNode == null)
            {
                Log.Error("Service XML does not contain Server Port node");
                return;
            }
            portNode.InnerText = Convert.ToString(port);
            xmlDoc.Save("Resources/ServiceDescription.xml");
            request.Body = xmlDoc.OuterXml;
            pua.SendRequest(request);
        }

        private static void Main()
        {
            if (String.IsNullOrEmpty(_localIP))
            {
                _localIP = Helpers.GetLocalIP();
            }
            TransportInfo localTransport = CreateTransport(_localIP, LocalPort);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += AppRequestRecvEvent;
            _app.ResponseRecvEvent += AppResponseRecvEvent;
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI("voicemail@open-ims.test");
            _localparty = new Address("<sip:voicemail@open-ims.test>");
            PublishService(true, LocalPort);
            StartTimer();
            Console.ReadKey();
        }
    }
}