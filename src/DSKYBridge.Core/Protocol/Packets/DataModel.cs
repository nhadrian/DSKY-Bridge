namespace DSKYBridge.Core.Protocol.Packets
{
    public sealed class DataModel
    {
        public int TargetCraft { get; set; }
        public int MessageType { get; set; }
        public int ID { get; set; }
        public int ToPos { get; set; }
    }
}
