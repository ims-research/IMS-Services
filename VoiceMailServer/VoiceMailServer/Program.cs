using System;
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

        private static PerformanceCounter _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private static PerformanceCounter _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
        private static PerformanceCounter _memCounter = new PerformanceCounter("Memory", "Available MBytes");
        private static PerformanceCounter _totalMemCounter = new PerformanceCounter("Memory", "Available MBytes", Process.GetCurrentProcess().ProcessName);

        private static float GetTotalCpuUsage(bool sleep = true)
        {
            if (sleep)
            {
                _totalCpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000);// 1 second wait   
            }
            return _totalCpuCounter.NextValue();
        }

        private static float GetCpuUsage(bool sleep = true)
        {
            if (sleep)
            {
                _cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _cpuCounter.NextValue();
        }

        private static float GetMemAvailable(bool sleep = true)
        {
            if (sleep)
            {
                _memCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _memCounter.NextValue();
        }

        private static float GetTotalMemAvailable(bool sleep = true)
        {
            if (sleep)
            {
                _memCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _memCounter.NextValue();
        }

        private static void GetResourceUsage(object sender, ElapsedEventArgs e)
        {
            GetCpuUsage(false);
            GetTotalCpuUsage(false);
            GetMemAvailable(false);
            GetTotalMemAvailable(false);
            System.Threading.Thread.Sleep(1000);
            float cpu = GetCpuUsage(false);
            float totalCPU =GetTotalCpuUsage(false);
            float mem = GetMemAvailable(false);
            float totalMem = GetTotalMemAvailable(false);
            UpdateServiceMetrics(cpu, totalCPU, mem, totalMem);
        }

        private static void StartTimer()
        {
            Timer aTimer = new Timer();
            aTimer.Elapsed += GetResourceUsage;
            aTimer.Interval = 30000;
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

        private static void UpdateServiceMetrics(float cpu, float totalCPU, float mem, float totalMem)
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
            node.InnerText = totalCPU.ToString();

            node = xmlDoc.SelectSingleNode("Service/Metrics/CPU");
            node.InnerText = cpu.ToString();

            node = xmlDoc.SelectSingleNode("Service/Metrics/TotalMemory");
            node.InnerText = totalMem.ToString();

            node = xmlDoc.SelectSingleNode("Service/Metrics/Memory");
            node.InnerText = mem.ToString();

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