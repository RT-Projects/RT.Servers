using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Direct;
using NUnit.Framework;
using System.Net.Sockets;
using System.Text;

[assembly: Timeout(40000)]

namespace RT.Servers.Tests
{
    sealed class ProgramServersTests
    {
        public static int Port = 12347;

        static void Main(string[] args)
        {
            bool wait = !args.Contains("--no-wait");

            NUnitDirect.RunTestsOnAssembly(Assembly.GetEntryAssembly());

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
