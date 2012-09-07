using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using SIPLib.SIP;
using SIPLib.Utils;

namespace VoiceMailServer
{
    class Program
    {
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

        static void Main(string[] args)
        {
            TransportInfo localTransport = CreateTransport(Helpers.GetLocalIP(), 5060);
            SIPApp app = new SIPApp(localTransport);
            const string pcscfIP = "192.168.0.7";
            const int pcscfPort = 4060;

            SIPStack stack = CreateStack(app, pcscfIP, pcscfPort);
            Console.ReadKey();
        }
    }
}
