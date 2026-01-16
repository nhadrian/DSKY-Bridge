using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DSKYBridge.Core.Reentry;
using DSKYBridge.Core.Protocol.LunarModule;

namespace DSKYBridge.Infrastructure.Reentry;

public sealed class UdpReentryCommandSender : IReentryCommandSender, IDisposable
{
    private readonly Socket _socket;
    private readonly EndPoint _endpoint;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public UdpReentryCommandSender(ReentryOptions options)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _endpoint = new IPEndPoint(IPAddress.Parse(options.Host), options.Port);
    }

    public Task SendKeyAsync(AgcKey key, bool isInCommandModule, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var packet = isInCommandModule ? BuildAgcPacket(key) : BuildLgcPacket(key);

        var json = JsonSerializer.Serialize(packet, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        _socket.SendTo(bytes, _endpoint);
        return Task.CompletedTask;
    }

    private static ReentryPacket BuildAgcPacket(AgcKey key)
    {
        return new ReentryPacket
        {
            TargetCraft = (int)ReentryEnums.Craft.CommandModule,
            MessageType = (int)ReentryEnums.MessageTypes.PushButton,
            ID = (int)MapAgcKeyToButtonId(key),
            ToPos = (int)ReentryEnums.PinPosition.NULL
        };
    }

    private static ReentryPacket BuildLgcPacket(AgcKey key)
    {
        return new ReentryPacket
        {
            TargetCraft = (int)ReentryEnums.Craft.LunarModule,
            MessageType = (int)ReentryEnums.MessageTypes.PushButton,
            // ReEntry expects LM button ids for the LGC when targeting the LunarModule craft.
            ID = (int)MapLgcKeyToButtonId(key),
            ToPos = (int)ReentryEnums.PinPosition.NULL
        };
    }

    private static ReentryEnums.CommandModuleButtonID MapAgcKeyToButtonId(AgcKey key) =>
        key switch
        {
            AgcKey.Verb => ReentryEnums.CommandModuleButtonID.AGCVerb,
            AgcKey.Noun => ReentryEnums.CommandModuleButtonID.AGCNoun,
            AgcKey.Plus => ReentryEnums.CommandModuleButtonID.AGCPluss,
            AgcKey.Minus => ReentryEnums.CommandModuleButtonID.AGCMinus,
            AgcKey.KeyRel => ReentryEnums.CommandModuleButtonID.AGCKeyRel,
            AgcKey.Pro => ReentryEnums.CommandModuleButtonID.AGCPro,
            AgcKey.Clear => ReentryEnums.CommandModuleButtonID.AGCClear,
            AgcKey.Enter => ReentryEnums.CommandModuleButtonID.AGCEntr,
            AgcKey.Reset => ReentryEnums.CommandModuleButtonID.AGCRset,
            AgcKey.D0 => ReentryEnums.CommandModuleButtonID.AGC0,
            AgcKey.D1 => ReentryEnums.CommandModuleButtonID.AGC1,
            AgcKey.D2 => ReentryEnums.CommandModuleButtonID.AGC2,
            AgcKey.D3 => ReentryEnums.CommandModuleButtonID.AGC3,
            AgcKey.D4 => ReentryEnums.CommandModuleButtonID.AGC4,
            AgcKey.D5 => ReentryEnums.CommandModuleButtonID.AGC5,
            AgcKey.D6 => ReentryEnums.CommandModuleButtonID.AGC6,
            AgcKey.D7 => ReentryEnums.CommandModuleButtonID.AGC7,
            AgcKey.D8 => ReentryEnums.CommandModuleButtonID.AGC8,
            AgcKey.D9 => ReentryEnums.CommandModuleButtonID.AGC9,
            _ => ReentryEnums.CommandModuleButtonID.NULL
        };

    private static LGCButtonId MapLgcKeyToButtonId(AgcKey key) =>
        key switch
        {
            AgcKey.Verb => LGCButtonId.LGCVerb,
            AgcKey.Noun => LGCButtonId.LGCNoun,
            AgcKey.Plus => LGCButtonId.LGCPlus,
            AgcKey.Minus => LGCButtonId.LGCMinus,
            AgcKey.KeyRel => LGCButtonId.LGCKeyRel,
            AgcKey.Pro => LGCButtonId.LGCPro,
            AgcKey.Clear => LGCButtonId.LGCClr,
            AgcKey.Enter => LGCButtonId.LGCEntr,
            AgcKey.Reset => LGCButtonId.LGCRset,
            AgcKey.D0 => LGCButtonId.LGC0,
            AgcKey.D1 => LGCButtonId.LGC1,
            AgcKey.D2 => LGCButtonId.LGC2,
            AgcKey.D3 => LGCButtonId.LGC3,
            AgcKey.D4 => LGCButtonId.LGC4,
            AgcKey.D5 => LGCButtonId.LGC5,
            AgcKey.D6 => LGCButtonId.LGC6,
            AgcKey.D7 => LGCButtonId.LGC7,
            AgcKey.D8 => LGCButtonId.LGC8,
            AgcKey.D9 => LGCButtonId.LGC9,
            _ => LGCButtonId.NULL
        };

    public void Dispose()
    {
        try { _socket?.Dispose(); } catch { /* ignore */ }
    }
}
