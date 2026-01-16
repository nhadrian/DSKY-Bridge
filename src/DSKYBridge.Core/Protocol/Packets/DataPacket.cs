namespace DSKYBridge.Core.Protocol.Packets
{
    /// <summary>
    /// Protocol metadata used to build packets for ReEntry.
    /// Keep these values stable (they map to ReEntry’s protocol).
    /// </summary>
    public static class DataPacket
    {
        // Craft IDs (match ReEntry)
        public enum Craft
        {
            Mercury = 0,
            Gemini = 1,
            CommandModule = 2,
            LunarModule = 3,
            SpaceShuttle = 4,
            Vostok = 5
        }

        // Message types (match ReEntry)
        public enum MessageTypes
        {
            SetSwitch = 0,
            PushButton = 1
        }

        // Default pin position (match ReEntry)
        public enum PinPosition
        {
            NULL = 0,
            Left,
            Middle,
            Right,
            Up,
            Down
        }
    }
}
