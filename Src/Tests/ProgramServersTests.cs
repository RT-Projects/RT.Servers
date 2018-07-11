using System;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using NUnit.Direct;
using NUnit.Framework;
using RT.Util.ExtensionMethods;

[assembly: Timeout(40000)]

namespace RT.Servers.Tests
{
    sealed class ProgramServersTests
    {
        public static int Port = 12347;

        static void Main(string[] args)
        {
            bool wait = !args.Contains("--no-wait");
            bool notimes = args.Contains("--no-times");

            string filter = null;
            var pos = args.IndexOf("--filter");
            if (pos != -1 && args.Length > pos + 1)
                filter = args[pos + 1];

            Console.OutputEncoding = Encoding.UTF8;
            NUnitDirect.RunTestsOnAssembly(Assembly.GetEntryAssembly(), notimes, filter);

            if (wait)
            {
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
            }
        }
    }

    static class TestUtil
    {
        public static void ReadResponseUntilContent(Socket sck)
        {
            byte[] b = new byte[65536];
            int bytesRead = sck.Receive(b);
            Assert.IsTrue(bytesRead > 0);
            string response = Encoding.UTF8.GetString(b, 0, bytesRead);
            while (!response.Contains("\r\n\r\n"))
            {
                bytesRead = sck.Receive(b);
                Assert.IsTrue(bytesRead > 0);
                response += Encoding.UTF8.GetString(b, 0, bytesRead);
            }
        }

    }
}
