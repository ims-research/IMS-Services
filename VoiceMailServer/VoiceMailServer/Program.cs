using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;

namespace VoiceMailServer
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));
        public static SIPStack CreateStack(SIPApp app, string proxyIp = null, int proxyPort = -1)
        {
            SIPStack myStack = new SIPStack(app) { Uri = { User = "alice" } };
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
        }

        static void AppRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            Log.Info("Request Received:" + e.Message);
        }

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 7000);
            SIPApp app = new SIPApp(localTransport);
            app.RequestRecvEvent += new EventHandler<SipMessageEventArgs>(AppRequestRecvEvent);
            app.ResponseRecvEvent += new EventHandler<SipMessageEventArgs>(AppResponseRecvEvent);
            const string scscfIP = "192.168.20.248";
            const int scscfPort = 4060;
            SIPStack stack = CreateStack(app, scscfIP, scscfPort);
            Console.ReadKey();
        }
    }
}
