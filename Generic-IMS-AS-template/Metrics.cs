using System.Collections.Generic;
using System.Diagnostics;

namespace $safeprojectname$
{
    class Metrics
    {
        private static PerformanceCounter _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private static PerformanceCounter _cpuCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
        private static PerformanceCounter _memCounter = new PerformanceCounter("Process", "Working Set - Private", Process.GetCurrentProcess().ProcessName);
        private static PerformanceCounter _memAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");

        public static float GetTotalCpuUsage(bool sleep = true)
        {
            if (sleep)
            {
                _totalCpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000);// 1 second wait   
            }
            return _totalCpuCounter.NextValue();
        }

        public static float GetCpuUsage(bool sleep = true)
        {
            if (sleep)
            {
                _cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _cpuCounter.NextValue();
        }

        public static float GetMemUsed(bool sleep = true)
        {
            if (sleep)
            {
                _memCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _memCounter.NextValue();
        }

        public static float GetMemAvailable(bool sleep = true)
        {
            if (sleep)
            {
                _memAvailableCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // 1 second wait
            }
            return _memAvailableCounter.NextValue();
        }

        public static Dictionary<string, float> GetResourceUsage()
        {
            GetCpuUsage(false);
            GetTotalCpuUsage(false);
            GetMemAvailable(false);
            GetMemUsed(false);
            System.Threading.Thread.Sleep(1000);
            float cpu = GetCpuUsage(false);
            float totalCPU = GetTotalCpuUsage(false);
            float memAvailable = GetMemAvailable(false);
            float memUsed = GetMemUsed(false);
            return new Dictionary<string, float>()
                {
                    {"cpu", cpu},
                    {"totalCPU", totalCPU},
                    {"memUsed", memUsed},
                    {"memAvailable", memAvailable}
                };
        }
    }
}
