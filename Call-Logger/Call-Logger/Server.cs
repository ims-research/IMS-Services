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

namespace Call_Logger
{
    static class Server
    {
        private static readonly ILog ConsoleLog = LogManager.GetLogger("ConsoleLog");
        private static readonly ILog SIPLog = LogManager.GetLogger("SIPLog");
        private static readonly ILog DebugLog = LogManager.GetLogger("DebugLog");
        private static readonly ILog ServiceLog = LogManager.GetLogger("ServiceLog");
        private static string _localIP = "192.168.20.25";
        private const int LocalPort = 9333;
        private static SIPApp _app;
        private const String ServerURI = "callLogger@open-ims.test";
        private static Address _localParty = new Address("<sip:" + ServerURI + ">");

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
                RemoteParty = new Address("<sip:" + ServerURI + ">"),
                LocalParty = _localParty
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
            ConsoleLog.Info(response.ResponseCode + " response received for " + requestType + " request");

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
                            //ConsoleLog.Warn("Received non 200 OK response");
                            //ConsoleLog.Warn(response);
                        }
                        break;
                    }
                case "INVITE":
                    {
                        Proxy pua = (Proxy)(e.UA);
                        RouteNewResponse(response, pua);
                        break;
                    }
                default:
                    ConsoleLog.Warn("Response for Request Type " + requestType + " is unhandled ");
                    break;
            }
        }

        private static void RouteNewMessage(Message request, Proxy pua)
        {

            SIPURI to = request.Uri;
            string toID = to.User + "@" + to.Host;

            Address from = (Address)(request.First("From").Value);
            string fromID = from.Uri.User + "@" + from.Uri.Host;

            Address dest = new Address(to.ToString());

            Message proxiedMessage = pua.CreateRequest(request.Method, dest, true, true);
            proxiedMessage.First("To").Value = dest;
            pua.SendRequest(proxiedMessage);
        }

        private static void RouteNewResponse(Message response, Proxy pua)
        {
            Message proxiedResponse = pua.CreateResponse(response.ResponseCode, response.ResponseText);
            pua.SendResponse(proxiedResponse);
        }

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Message request = e.Message;

            SIPLog.Info(request);
            ConsoleLog.Info(request.Method.ToUpper() + " request received");

            switch (request.Method.ToUpper())
            {
                case "INVITE":
                    {
                        LogCallDetails(request);
                        Message m = e.UA.CreateResponse(183, "Call Is Being Forwarded");
                        e.UA.SendResponse(m);
                        Proxy pua = (Proxy)(e.UA);
                        RouteNewMessage(request, pua);
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

        private static void LogCallDetails(Message request)
        {
            // Stub for logging call details
            DebugLog.Info("Received call from "+request.First("From"));
        }

        private static void PublishService(bool determineIP, int port)
        {
            UserAgent pua = new UserAgent(_app.Stack) { RemoteParty = new Address("<sip:" + ServerURI + ">"), LocalParty = _localParty };
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
            _app.RequestRecvEvent += AppRequestRecvEvent;
            _app.ResponseRecvEvent += AppResponseRecvEvent;
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI(ServerURI);
            //PublishService(true, LocalPort);
            //StartTimer();
            Console.ReadKey();
        }

    }
}
