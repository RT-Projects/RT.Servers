using System.IO;
using System.Net.Sockets;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RT.Servers.Tests
{
    static class TestHelpers
    {
        public static int Port = 12347;

        public static byte[] ReceiveAllBytes(this Socket socket)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

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
