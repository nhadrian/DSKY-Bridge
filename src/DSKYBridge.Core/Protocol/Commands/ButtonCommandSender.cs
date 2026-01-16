using System.Text.Json;
using DSKYBridge.Core.Protocol.Packets;
using DSKYBridge.Core.Protocol.Transport;

namespace DSKYBridge.Core.Protocol.Commands
{
    internal static class ButtonCommandSender
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        public static void Send(
            DataPacket.Craft craft,
            int buttonId)
        {
            var packet = new DataModel
            {
                TargetCraft = (int)craft,
                MessageType = (int)DataPacket.MessageTypes.PushButton,
                ID = buttonId,
                ToPos = (int)DataPacket.PinPosition.NULL
            };

            string json = JsonSerializer.Serialize(packet, JsonOptions);
            UdpCommandSender.Send(json);
        }
    }
}
