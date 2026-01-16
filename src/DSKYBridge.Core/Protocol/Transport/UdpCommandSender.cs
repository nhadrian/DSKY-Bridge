using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DSKYBridge.Core.Protocol.Transport
{
    internal static class UdpCommandSender
    {
        private static readonly IPEndPoint Endpoint =
            new(IPAddress.Loopback, 8051);

        public static void Send(string json)
        {
            using var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            byte[] buffer = Encoding.UTF8.GetBytes(json);
            socket.SendTo(buffer, Endpoint);
        }
    }
}
