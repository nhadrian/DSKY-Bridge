using DSKYBridge.Core.Protocol.CommandModule;
using DSKYBridge.Core.Protocol.LunarModule;
using DSKYBridge.Core.Protocol.Commands;
using DSKYBridge.Core.Protocol.Packets;

namespace DSKYBridge.Core.Commands
{
    public static class StoredCmds
    {
        // ============================
        // AGC (Command Module)
        // ============================

        private static void SendAGC(AGCButtonId id) =>
            ButtonCommandSender.Send(
                DataPacket.Craft.CommandModule,
                (int)id);

        public static void AGCVerb()   => SendAGC(AGCButtonId.AGCVerb);
        public static void AGCNoun()   => SendAGC(AGCButtonId.AGCNoun);
        public static void AGCPlus()   => SendAGC(AGCButtonId.AGCPlus);
        public static void AGCMinus()  => SendAGC(AGCButtonId.AGCMinus);

        public static void AGC0() => SendAGC(AGCButtonId.AGC0);
        public static void AGC1() => SendAGC(AGCButtonId.AGC1);
        public static void AGC2() => SendAGC(AGCButtonId.AGC2);
        public static void AGC3() => SendAGC(AGCButtonId.AGC3);
        public static void AGC4() => SendAGC(AGCButtonId.AGC4);
        public static void AGC5() => SendAGC(AGCButtonId.AGC5);
        public static void AGC6() => SendAGC(AGCButtonId.AGC6);
        public static void AGC7() => SendAGC(AGCButtonId.AGC7);
        public static void AGC8() => SendAGC(AGCButtonId.AGC8);
        public static void AGC9() => SendAGC(AGCButtonId.AGC9);

        public static void AGCClear()  => SendAGC(AGCButtonId.AGCClear);
        public static void AGCPro()    => SendAGC(AGCButtonId.AGCPro);
        public static void AGCKeyRel() => SendAGC(AGCButtonId.AGCKeyRel);
        public static void AGCEntr()   => SendAGC(AGCButtonId.AGCEntr);
        public static void AGCRset()   => SendAGC(AGCButtonId.AGCRset);

        // ============================
        // LGC (Lunar Module)
        // ============================

        private static void SendLGC(LGCButtonId id) =>
            ButtonCommandSender.Send(
                DataPacket.Craft.LunarModule,
                (int)id);

        public static void LGCVerb()   => SendLGC(LGCButtonId.LGCVerb);
        public static void LGCNoun()   => SendLGC(LGCButtonId.LGCNoun);
        public static void LGCPlus()   => SendLGC(LGCButtonId.LGCPlus);
        public static void LGCMinus()  => SendLGC(LGCButtonId.LGCMinus);

        public static void LGC0() => SendLGC(LGCButtonId.LGC0);
        public static void LGC1() => SendLGC(LGCButtonId.LGC1);
        public static void LGC2() => SendLGC(LGCButtonId.LGC2);
        public static void LGC3() => SendLGC(LGCButtonId.LGC3);
        public static void LGC4() => SendLGC(LGCButtonId.LGC4);
        public static void LGC5() => SendLGC(LGCButtonId.LGC5);
        public static void LGC6() => SendLGC(LGCButtonId.LGC6);
        public static void LGC7() => SendLGC(LGCButtonId.LGC7);
        public static void LGC8() => SendLGC(LGCButtonId.LGC8);
        public static void LGC9() => SendLGC(LGCButtonId.LGC9);

        public static void LGCClr()    => SendLGC(LGCButtonId.LGCClr);
        public static void LGCPro()    => SendLGC(LGCButtonId.LGCPro);
        public static void LGCKeyRel() => SendLGC(LGCButtonId.LGCKeyRel);
        public static void LGCEntr()   => SendLGC(LGCButtonId.LGCEntr);
        public static void LGCRset()   => SendLGC(LGCButtonId.LGCRset);
    }
}
