using System.Threading;
using System.Threading.Tasks;

namespace DSKYBridge.Core.Reentry;

public interface IReentryCommandSender
{
    /// <summary>
    /// Sends a DSKY-style key press to ReEntry.
    /// When <paramref name="isInCommandModule"/> is true, the key is routed to the AGC (CM).
    /// When false, it is routed to the LGC (LM).
    /// </summary>
    Task SendKeyAsync(AgcKey key, bool isInCommandModule, CancellationToken ct = default);

    // Backwards-compatible helpers
    Task SendAgcKeyAsync(AgcKey key, CancellationToken ct = default) => SendKeyAsync(key, isInCommandModule: true, ct);
    Task SendLgcKeyAsync(AgcKey key, CancellationToken ct = default) => SendKeyAsync(key, isInCommandModule: false, ct);
}
