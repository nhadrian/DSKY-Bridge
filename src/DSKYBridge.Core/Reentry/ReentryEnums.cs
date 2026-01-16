namespace DSKYBridge.Core.Reentry;

public static class ReentryEnums
{
    public enum Craft
    {
        Mercury,
        Gemini,
        CommandModule,
        LunarModule,
        SpaceShuttle,
        Vostok
    }

    public enum MessageTypes
    {
        SetSwitch,
        PushButton
    }

    public enum PinPosition
    {
        NULL, Left, Middle, Right, Up, Down
    }

    public enum CommandModuleButtonID
    {
        NULL,
        AGCVerb, AGCNoun, AGCPluss, AGCMinus,
        AGC0, AGC1, AGC2, AGC3, AGC4, AGC5, AGC6, AGC7, AGC8, AGC9,
        AGCClear, AGCPro, AGCKeyRel, AGCEntr, AGCRset
    }
}
