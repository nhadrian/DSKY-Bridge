namespace DSKYBridge.Core.Reentry;

public sealed class ReentryPacket
{
    public int TargetCraft { get; set; }
    public int MessageType { get; set; }
    public int ID { get; set; }
    public int ToPos { get; set; }
}
