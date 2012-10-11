using System;
using System.Net;
using System.Xml;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;

namespace VoiceMailServer
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static readonly ILog SessionLog = LogManager.GetLogger("SessionLogger");
        private static SIPApp _app;
        private static Address _localparty;

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
            return new TransportInfo(IPAddress.Parse(listenIp), listenPort, System.Net.Sockets.ProtocolType.Udp);
        }

        static void AppResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Response Received:"+e.Message);
            Message response = e.Message;
            string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();
            switch (requestType)
            {
                case "INVITE":
                case "REGISTER":
                case "BYE":
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

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Request Received:" + e.Message);
            Message request = e.Message;
            switch (request.Method.ToUpper())
            {
                case "INVITE":
                    {
                        SessionLog.Info("Call received from " +request.First("From"));
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
                case "ACK":
                case "MESSAGE":
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

        private static void PublishService()
        {
            UserAgent pua = new UserAgent(_app.Stack) { RemoteParty = new Address("<sip:voicemail@open-ims.test>"), LocalParty = _localparty };
            Message request = pua.CreateRequest("PUBLISH");
            request.InsertHeader(new Header("service.description", "Event"));
            request.InsertHeader(new Header("application/SERV_DESC+xml", "Content-Type"));
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("Resources/ServiceDescription.xml");
            request.Body = xmlDoc.OuterXml;
            pua.SendRequest(request);
        }

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 7000);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            const string scscfIP = "scscf.open-ims.test";
            const int scscfPort = 6060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI("voicemail@open-ims.test");
            _localparty = new Address("<sip:voicemail@open-ims.test>");
            PublishService();
            Console.ReadKey();
        }
    }
}
