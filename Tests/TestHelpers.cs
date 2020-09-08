using System.IO;
using System.Net.Sockets;

namespace RT.Servers.Tests
{
    static class TestHelpers
    {
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
    }
}
