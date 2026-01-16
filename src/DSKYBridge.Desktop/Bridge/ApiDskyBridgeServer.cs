using System;
using System.Collections.Generic;
using System.Linq;
using Fleck;
using System.Text.Json;
using System.Threading.Tasks;
using DSKYBridge.Core.Reentry;

namespace DSKYBridge.Desktop.Bridge
{
    public sealed class ApiDskyBridgeServer : IDisposable
    {
    private readonly WebSocketServer _server;
    private readonly List<IWebSocketConnection> _clients = new();
    private readonly object _syncRoot = new();

    private readonly IReentryCommandSender _commandSender;
    private readonly Func<bool> _isInCommandModuleProvider;

    private string? _lastStateJson;

        // 🔔 Events for UI
        public event Action<string>? ClientConnected;
        public event Action? ClientDisconnected;

        public ApiDskyBridgeServer(
            IReentryCommandSender commandSender,
            Func<bool> isInCommandModuleProvider,
            string url = "ws://0.0.0.0:3001")   // listen on all interfaces, port 3001
        {
            _commandSender = commandSender;
            _isInCommandModuleProvider = isInCommandModuleProvider;

            _server = new WebSocketServer(url)
            {
                // Optional: explicitly support the "echo-protocol" subprotocol used by api-dsky
                SupportedSubProtocols = new[] { "echo-protocol" }
            };
        }

        public void Start()
        {
            // Start the WebSocket server. This returns immediately; Fleck handles connections in the background.
            _server.Start(socket =>
            {
                // NEW CLIENT CONNECTED
                socket.OnOpen = () =>
                {
                    lock (_syncRoot)
                    {
                        _clients.Add(socket);
                    }

                    var ip = socket.ConnectionInfo.ClientIpAddress;
                    ClientConnected?.Invoke(ip);
                    Console.WriteLine($"[Bridge] Client connected from {ip}");

                    // Optionally: send last known state immediately
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
                    lock (_syncRoot)
                    {
                        _clients.Remove(socket);
                    }

                    ClientDisconnected?.Invoke();
                    Console.WriteLine("[Bridge] Client disconnected");
                };

                // ERROR ON CONNECTION
                socket.OnError = ex =>
                {
                    Console.WriteLine($"[Bridge] WebSocket error: {ex.Message}");
                    // Treat any error as a disconnect from the UI perspective
                    ClientDisconnected?.Invoke();
                };

                // MESSAGE FROM CLIENT (keyboard input)
                socket.OnMessage = async message =>
                {
                    // message is string (UTF-8 text) – this matches what api-dsky sends
                    try
                    {
                        await HandleKeyPressAsync(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Bridge] Error handling key press: {ex.Message}");
                    }
                };
            });

            Console.WriteLine("[Bridge] WebSocket server listening on ws://0.0.0.0:3001");
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

        private async Task HandleKeyPressAsync(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return;

            char keyChar = char.ToLowerInvariant(payload.Trim()[0]);

            if (!TryMapCharToAgcKey(keyChar, out var agcKey))
                return;

            bool isInCommandModule = _isInCommandModuleProvider();

            try
            {
                await _commandSender.SendKeyAsync(agcKey, isInCommandModule);
                Console.WriteLine($"[Bridge] Key '{keyChar}' -> {(isInCommandModule ? "CMC" : "LMC")}");
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
