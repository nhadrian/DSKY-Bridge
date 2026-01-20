using System;
using System.Collections.Generic;
using System.Linq;
using Fleck;
using System.Text.Json;
using System.Threading.Tasks;
using DSKYBridge.Core.Reentry;
using System.Threading;

namespace DSKYBridge.Desktop.Bridge
{
    public sealed class ApiDskyBridgeServer : IDisposable
    {
        private readonly WebSocketServer _server;
        private readonly List<IWebSocketConnection> _clients = new();
        private readonly object _syncRoot = new();

        private readonly IReentryCommandSender _commandSender;
        private readonly Func<string, bool> _isInCommandModuleProvider;

        private string? _lastStateJson;

        // 🔔 Events for UI
        public event Action<string>? ClientConnected;
        public event Action<string>? ClientDisconnected;
        private readonly Dictionary<IWebSocketConnection, int> _clientSlots = new();

        public ApiDskyBridgeServer(
            IReentryCommandSender commandSender,
            Func<string, bool> isInCommandModuleProvider,
            string url = "ws://0.0.0.0:3000")   // listen on all interfaces, port 3000
        {
            _commandSender = commandSender;
            _isInCommandModuleProvider = isInCommandModuleProvider;

            // Accept connections from any WebSocket client (no subprotocol requirement).
            _server = new WebSocketServer(url);
        }

        public void Start()
        {
            _server.Start(socket =>
            {
                // NEW CLIENT CONNECTED
                socket.OnOpen = () =>
                {
                    int slot;

                    lock (_syncRoot)
                    {
                        // Assign the lowest free slot (1 or 2)
                        var taken = _clientSlots.Values.ToList();

                        if (!taken.Contains(1))      slot = 1;
                        else if (!taken.Contains(2)) slot = 2;
                        else                         slot = 2; // both used; reuse 2 as a fallback

                        _clientSlots[socket] = slot;
                        _clients.Add(socket);
                    }

                    var ip = socket.ConnectionInfo.ClientIpAddress;
                    ClientConnected?.Invoke(ip);
                    Console.WriteLine($"[Bridge] Client connected from {ip} -> slot {slot}");

                    if (!string.IsNullOrEmpty(_lastStateJson))
                    {
                        try
                        {
                            socket.Send(_lastStateJson);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Bridge] Error sending initial state: {ex.Message}");
                        }
                    }
                };

                // CLIENT DISCONNECTED
                socket.OnClose = () =>
                {
                    int slot;
                    var ip = socket.ConnectionInfo.ClientIpAddress;

                    lock (_syncRoot)
                    {
                        _clients.Remove(socket);

                        if (!_clientSlots.TryGetValue(socket, out slot))
                            slot = 0;

                        _clientSlots.Remove(socket);
                    }

                    ClientDisconnected?.Invoke(ip);
                    Console.WriteLine($"[Bridge] Client disconnected ({ip}) from slot {slot}");
                };

                // ERROR ON CONNECTION
                socket.OnError = ex =>
                {
                    Console.WriteLine($"[Bridge] WebSocket error: {ex.Message}");
                    var ip = socket.ConnectionInfo.ClientIpAddress;

                    lock (_syncRoot)
                    {
                        _clients.Remove(socket);
                        _clientSlots.Remove(socket);
                    }

                    ClientDisconnected?.Invoke(ip);
                };

                // MESSAGE FROM CLIENT (keyboard input)
                socket.OnMessage = async message =>
                {
                    int slot;
                    lock (_syncRoot)
                    {
                        if (!_clientSlots.TryGetValue(socket, out slot))
                            slot = 1; // default to slot 1 if something went wrong
                    }

                    var slotId = slot.ToString(); // "1" or "2"

                    try
                    {
                        await HandleKeyPressAsync(message, slotId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Bridge] Error handling key press from slot {slotId}: {ex.Message}");
                    }
                };
            });

            Console.WriteLine("[Bridge] WebSocket server listening on ws://0.0.0.0:3000");
        }

        public Task BroadcastStateAsync(object state, CancellationToken ct = default)
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null // or whatever you used before
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] Failed to serialize state: {ex.Message}");
                return Task.CompletedTask;
            }

            if (json == _lastStateJson)
                return Task.CompletedTask;

            _lastStateJson = json;

            List<IWebSocketConnection> clients;
            lock (_syncRoot)
            {
                clients = _clients.ToList();
            }

            foreach (var client in clients)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!client.IsAvailable)
                    continue;

                try
                {
                    client.Send(json);  // Fleck send (synchronous)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Bridge] Error sending state to client: {ex.Message}");
                }
            }

            return Task.CompletedTask;
        }

        public Task BroadcastStatePerSlotAsync(Func<int, object> stateSelector, CancellationToken ct = default)
        {
            List<(IWebSocketConnection socket, int slot)> clients;

            lock (_syncRoot)
            {
                clients = _clients
                    .Select(c =>
                    {
                        int slot = 1;
                        if (!_clientSlots.TryGetValue(c, out slot))
                            slot = 1;
                        return (c, slot);
                    })
                    .ToList();
            }

            foreach (var (client, slot) in clients)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!client.IsAvailable)
                    continue;

                object stateObj;
                string json;

                try
                {
                    stateObj = stateSelector(slot);
                    json = JsonSerializer.Serialize(stateObj, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = null
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Bridge] Failed to build or serialize state for slot {slot}: {ex.Message}");
                    continue;
                }

                // Remember the last JSON globally so new connections
                // can get a snapshot in OnOpen if needed.
                _lastStateJson = json;

                try
                {
                    client.Send(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Bridge] Error sending state to client in slot {slot}: {ex.Message}");
                }
            }

            return Task.CompletedTask;
        }

        private async Task HandleKeyPressAsync(string payload, string clientSlotId)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;

            char keyChar = char.ToLowerInvariant(payload.Trim()[0]);

            if (!TryMapCharToAgcKey(keyChar, out var agcKey))
                return;

            bool isInCommandModule = _isInCommandModuleProvider(clientSlotId);

            try
            {
                await _commandSender.SendKeyAsync(agcKey, isInCommandModule);
                Console.WriteLine($"[Bridge] Key '{keyChar}' from slot {clientSlotId} -> {(isInCommandModule ? "CMC" : "LMC")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bridge] Failed to send key: {ex.Message}");
            }
        }

        private static bool TryMapCharToAgcKey(char ch, out AgcKey key)
        {
            key = default;
            switch (ch)
            {
                case 'v': key = AgcKey.Verb;  return true;
                case 'n': key = AgcKey.Noun;  return true;
                case '+': key = AgcKey.Plus;  return true;
                case '-': key = AgcKey.Minus; return true;
                case 'k': key = AgcKey.KeyRel;return true;
                case 'p': key = AgcKey.Pro;   return true;
                case 'c': key = AgcKey.Clear; return true;
                case 'e': key = AgcKey.Enter; return true;
                case 'r': key = AgcKey.Reset; return true;
                case '0': key = AgcKey.D0;    return true;
                case '1': key = AgcKey.D1;    return true;
                case '2': key = AgcKey.D2;    return true;
                case '3': key = AgcKey.D3;    return true;
                case '4': key = AgcKey.D4;    return true;
                case '5': key = AgcKey.D5;    return true;
                case '6': key = AgcKey.D6;    return true;
                case '7': key = AgcKey.D7;    return true;
                case '8': key = AgcKey.D8;    return true;
                case '9': key = AgcKey.D9;    return true;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                foreach (var c in _clients.ToList())
                {
                    try { c.Close(); } catch { /* ignore */ }
                }
                _clients.Clear();
            }

            _server.Dispose();
        }
    }
}
