using System;
using System.Net;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;

namespace VoiceMailServer
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        private static SIPApp _app;
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
                        
                        
                        _app.Useragents.Add(e.UA);
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                case "BYE":
                    {
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

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 7000);
            _app = new SIPApp(localTransport);
            _app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            _app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            const string scscfIP = "192.168.20.248";
            const int scscfPort = 4060;
            SIPStack stack = CreateStack(_app, scscfIP, scscfPort);
            stack.Uri = new SIPURI("voicemail@open-ims.test");
            Console.ReadKey();
        }
    }
}
