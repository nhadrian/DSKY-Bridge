using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using DSKYBridge.Core.Reentry;
using DSKYBridge.Infrastructure.Reentry;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using DSKYBridge.Desktop.Bridge;
using System.Linq;
using Makaretu.Dns;
using System.Windows.Controls;
using System.Collections.Generic;

namespace DSKYBridge
{
    public partial class MainWindow : Window
    {
        private const double DesignWidth = 800.0;
        private const double DesignHeight = 500.0;
        private double _aspectRatio = DesignWidth / DesignHeight;
        private bool _isAdjustingSize;
        private bool _isInResizeMove;
        private bool _ignoreNextSizeChanged;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApiDskyBridgeServer _bridgeServer;
        private readonly DispatcherTimer _pollTimer;
        private bool _readerRunning;
        private int _dskyMode = 1;
        private int _dskyMode2 = 1;
                // Which client IP is which DSKY slot
        private string? _client1Ip;
        private string? _client2Ip;

        // Currently selected local IP address (override-able by the user)
        private IPAddress? _selectedLocalIp;

        // For reusing mDNS instance name and hostname when we re-advertise
        private string? _mdnsInstanceName;
        private string? _mdnsHostName;

        // List of IP options shown in the overlay menu
        private readonly List<IpOption> _ipOptions = new();
        private const string ImgBase = "pack://application:,,,/Assets/Images/";

        private const double NormalWidth = 800.0;
        private const double NormalHeight = 500.0;
        private const double ExtendedWidth = 800.0;
        private const double ExtendedHeight = 700.0;

        private string? _primaryDskyIp;
        private string? _secondaryDskyIp;
        private bool _hasSecondClient;
        private const int WebSocketPort = 3000;

        // TODO: set this to whatever service type next-dsky actually browses for.
        // This is a *guess*; check the next-dsky docs/source.
        private const string MdnsServiceType = "_dsky._tcp";

        private ServiceDiscovery? _mdns;
        private ServiceProfile? _mdnsProfile;

        /// Check whether the Reentry UDP port (127.0.0.1:8051) currently has a listener.
        private static bool IsReentryPortOpen()
        {
            try
            {
                var ipGlobal = IPGlobalProperties.GetIPGlobalProperties();
                var listeners = ipGlobal.GetActiveUdpListeners();

                foreach (var ep in listeners)
                {
                    if (ep.Port == 8051 &&
                        (ep.Address.Equals(IPAddress.Loopback) || ep.Address.Equals(IPAddress.Any)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Swallow exceptions and treat as "not open";
                // indicators will simply show error in that case.
            }

            return false;
        }

        /// <summary>
        /// Update the small Reentry link indicator LEDs in the UI.
        /// Uplink  : Reentry UDP port listener state.
        /// Downlink: JSON reader / export files state.
        /// </summary>
        private void UpdateLinkIndicators()
        {
            // Uplink indicator: REENTRY UDP port on localhost:8051
            bool portOpen = IsReentryPortOpen();
            UpLink.Source = new BitmapImage(new Uri(ImgBase + (portOpen ? "ind_ok.png" : "ind_error.png")));

            // Downlink indicator: JSON reader status (_readerRunning)
            DownLink.Source = new BitmapImage(new Uri(ImgBase + (_readerRunning ? "ind_ok.png" : "ind_error.png")));
        }

        // Last-read values for AGC and LGC (available for future use)
        private CMCValues? _lastAgcValues;
        private LGCValues? _lastLgcValues;

        // Opening this sender on startup opens the Reentry UDP port.
        private readonly IReentryCommandSender _commandSender =
            new UdpReentryCommandSender(new ReentryOptions { Host = "127.0.0.1", Port = 8051 });

        //ipv4 formatting helper
        private static string FormatIpBlocks(string? ipv4)
        {
            // Default when no usable IP is available
            if (string.IsNullOrWhiteSpace(ipv4))
                return "000 000 000 000";

            var parts = ipv4.Split('.');
            if (parts.Length != 4)
                return "000 000 000 000";

            return string.Join(" ",
                parts.Select(p =>
                    int.TryParse(p, out var n)
                        ? n.ToString("000")
                        : "000"));
        }

        // Get the local IPv4 address formatted for display.
        private static string GetDisplayPrimaryIPv4()
        {
            var ip = GetPrimaryUnicastIPv4Address();
            return FormatIpBlocks(ip?.ToString());
        }

        private sealed class IpOption
        {
            public required string AdapterName { get; init; }
            public required IPAddress Address { get; init; }

            public string Display => $"{Address} - {AdapterName}";
        }

        /// Handle a new api-dsky client connection.
        private void OnBridgeClientConnected(string ip)
        {
            var formatted = FormatIpBlocks(ip);

            if (_primaryDskyIp == null)
            {
                // First client → main row
                _primaryDskyIp = ip;
                DskyIP.Content = formatted;
            }
            else if (_secondaryDskyIp == null)
            {
                // Second client → second row
                _secondaryDskyIp = ip;
                DskyIP2.Content = formatted;
            }
            else
            {
                // Already two clients; reuse the second slot
                _secondaryDskyIp = ip;
                DskyIP2.Content = formatted;
            }

            _hasSecondClient = _secondaryDskyIp != null;
            ApplyClientLayout();
        }

        private void OnBridgeClientDisconnected(string ip)
        {
            bool changed = false;

            if (_primaryDskyIp != null &&
                string.Equals(_primaryDskyIp, ip, StringComparison.OrdinalIgnoreCase))
            {
                if (_secondaryDskyIp != null)
                {
                    // Promote the second slot to primary if it exists
                    _primaryDskyIp = _secondaryDskyIp;
                    DskyIP.Content = DskyIP2.Content;

                    _secondaryDskyIp = null;
                    DskyIP2.Content = "000 000 000 000";
                }
                else
                {
                    _primaryDskyIp = null;
                    DskyIP.Content = "000 000 000 000";
                }

                changed = true;
            }
            else if (_secondaryDskyIp != null &&
                    string.Equals(_secondaryDskyIp, ip, StringComparison.OrdinalIgnoreCase))
            {
                _secondaryDskyIp = null;
                DskyIP2.Content = "000 000 000 000";
                changed = true;
            }

            if (changed)
            {
                _hasSecondClient = _secondaryDskyIp != null;
                ApplyClientLayout();
            }
        }

        private void ApplyClientLayout()
        {
            if (_hasSecondClient)
            {
                // Show the second DSKY control row and grow the main design surface
                DskyControl2.Visibility = Visibility.Visible;

                RootGrid.Height = ExtendedHeight;
                _aspectRatio = ExtendedWidth / ExtendedHeight;

                // Snap the window to the requested 800x700
                Width = ExtendedWidth;
                Height = ExtendedHeight;
            }
            else
            {
                // Only one (or zero) clients → collapse the second row
                DskyControl2.Visibility = Visibility.Collapsed;

                RootGrid.Height = NormalHeight;
                _aspectRatio = NormalWidth / NormalHeight;

                Width = NormalWidth;
                Height = NormalHeight;
            }

            // Make sure the aspect-ratio logic doesn't immediately fight this change
            _ignoreNextSizeChanged = true;
            EnforceAspectRatioOnce();
        }

        /// Attempt to get the primary IPv4 address of the machine.
        private static IPAddress? GetPrimaryUnicastIPv4Address()
        {
            // 1) Prefer the interface that has the default route (has a gateway)
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                // This doesn't actually send traffic, it just forces routing decision
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint ep)
                    return ep.Address;
            }
            catch
            {
                // ignore and fall back
            }

            // 2) Fallback: pick an interface that has an IPv4 gateway
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                var props = ni.GetIPProperties();

                // require an IPv4 gateway (this is your "has gateway" filter)
                bool hasIpv4Gateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(g.Address) &&
                    !g.Address.Equals(IPAddress.Any) &&
                    !g.Address.Equals(IPAddress.None));

                if (!hasIpv4Gateway)
                    continue;

                var ipInfo = props.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipInfo != null)
                    return ipInfo.Address;
            }

            // Nothing suitable found
            return null;
        }

        public MainWindow()
        {
            InitializeComponent();

            // Force a known starting size, independent of XAML and layout quirks
            Loaded += (_, _) =>
            {
                // Start from design size
                Width  = DesignWidth;
                Height = DesignHeight;
                _aspectRatio = DesignWidth / DesignHeight;

                _ignoreNextSizeChanged = true;
                EnforceAspectRatioOnce();

                //Console.WriteLine($"[Loaded] Window = {ActualWidth} x {ActualHeight}");
                //Console.WriteLine($"[Loaded] Content = {RootGrid.ActualWidth} x {RootGrid.ActualHeight}");
            };

            // At this point, all XAML controls (including LocalIP) are created, so it's safe to use them.
            _selectedLocalIp = GetPrimaryUnicastIPv4Address();
            LocalIP.Content = FormatIpBlocks(_selectedLocalIp?.ToString());
            DskyIP.Content = "000 000 000 000";
            DskyIP2.Content = "000 000 000 000";

            MouseDown += Window_MouseDown;
            SizeChanged += Window_SizeChanged; // aspect-ratio enforcement (works even without XAML hook)
    
            // Improve resize smoothness: avoid constantly fighting the user's drag.
            // We only enforce the aspect ratio once the user finishes resizing.
            SourceInitialized += (_, _) =>
            {
                if (PresentationSource.FromVisual(this) is HwndSource src)
                    src.AddHook(WndProc);
            };

            _bridgeServer = new ApiDskyBridgeServer(
                _commandSender,
                GetIsInCommandModuleForClient,   // NEW: per-client mode
                url: $"ws://0.0.0.0:{WebSocketPort}");

            try
            {
                _mdns = new ServiceDiscovery();

                _mdnsHostName    = System.Net.Dns.GetHostName();
                _mdnsInstanceName = $"DSKY Bridge ({_mdnsHostName})";

                // Use the currently selected local IP (from startup or user override)
                if (_selectedLocalIp is null)
                {
                    _selectedLocalIp = GetPrimaryUnicastIPv4Address();
                }

                UpdateMdnsAdvertisedAddress(_selectedLocalIp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mDNS] Failed to start mDNS advertising: {ex.Message}");
            }

            // When api-dsky clients connect, assign them to slot 1 or 2
            _bridgeServer.ClientConnected += ip =>
            {
                Dispatcher.Invoke(() =>
                {
                    var formatted = FormatIpBlocks(ip);

                    if (_client1Ip == null)
                    {
                        _client1Ip = ip;
                        DskyIP.Content = formatted;
                    }
                    else if (_client2Ip == null)
                    {
                        _client2Ip = ip;
                        DskyIP2.Content = formatted;

                        // You already have logic to show DskyControl2 + resize;
                        // if not, this is a safe place to do it:
                        DskyControl2.Visibility = Visibility.Visible;

                        RootGrid.Height = 700;
                        _aspectRatio = 800.0 / 700.0;
                        Width = 800.0;
                        Height = 700.0;
                        _ignoreNextSizeChanged = true;
                        EnforceAspectRatioOnce();
                    }
                    else
                    {
                        // More than two clients: ignore or reuse a slot if you like
                        Console.WriteLine($"[DSKY-Bridge] Extra client connected from {ip}, but both slots are already used.");
                    }
                });
            };

            // If your ApiDskyBridgeServer.ClientDisconnected still has no IP parameter,
            // you can leave this as-is or adjust once you add the IP there.
            // For _dskyMode2 routing we don't strictly need to clear the mapping.
            _bridgeServer.ClientDisconnected += ip =>
            {
                Dispatcher.Invoke(() =>
                {
                    // FIRST connection (slot 1) broke
                    if (_client1Ip != null &&
                        string.Equals(_client1Ip, ip, StringComparison.OrdinalIgnoreCase))
                    {
                        _client1Ip = null;
                        DskyIP.Content = "000 000 000 000";
                        // We do NOT touch DskyControl2 here – second DSKY can stay visible
                    }
                    // SECOND connection (slot 2) broke
                    else if (_client2Ip != null &&
                            string.Equals(_client2Ip, ip, StringComparison.OrdinalIgnoreCase))
                    {
                        _client2Ip = null;
                        DskyIP2.Content = "000 000 000 000";

                        // Hide the second DSKY grid and revert to 800×500
                        DskyControl2.Visibility = Visibility.Collapsed;

                        RootGrid.Height = NormalHeight;
                        _aspectRatio = NormalWidth / NormalHeight;

                        Width = NormalWidth;
                        Height = NormalHeight;
                        _ignoreNextSizeChanged = true;
                        EnforceAspectRatioOnce();
                    }

                    // Optional: refresh the link LEDs after a disconnect
                    UpdateLinkIndicators();
                });
            };

            _bridgeServer.Start();

            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _pollTimer.Tick += async (_, _) => await PollOnceAsync();

            // Start JSON reader on app start.
            InitializeJsonReader();
        }

        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE  = 0x0232;

        private const int WM_NCHITTEST     = 0x0084;

        private const int HTCLIENT      = 1;
        private const int HTTOPLEFT     = 13;
        private const int HTTOPRIGHT    = 14;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;

        private const int CMC = 2;
        private const int AUTO = 1;
        private const int LMC = 0;

        private double EffectiveResizeBorder =>
            Math.Clamp(Math.Min(Width, Height) * 0.03, 14, 32); // 2% of min dimension, between 8–24 px

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    _isInResizeMove = true;
                    break;

                case WM_EXITSIZEMOVE:
                    _isInResizeMove = false;
                    _ignoreNextSizeChanged = true; // prevent stale SizeChanged applying an older size
                    EnforceAspectRatioOnce();
                    break;

                case WM_NCHITTEST:
                    handled = true;
                    return HitTestNCA(lParam);
            }

            return IntPtr.Zero;
        }

        private IntPtr HitTestNCA(IntPtr lParam)
        {
            // Screen coords packed into lParam
            int x = (short)((uint)lParam & 0xFFFF);
            int y = (short)(((uint)lParam >> 16) & 0xFFFF);

            Point p = PointFromScreen(new Point(x, y));
            double border = EffectiveResizeBorder;

            // If RootGrid is available, use its bounds as the "background" edge
            if (RootGrid != null && RootGrid.IsLoaded)
            {
                var rect = RootGrid
                    .TransformToAncestor(this)
                    .TransformBounds(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));

                bool leftEdge   = p.X <= rect.Left  + border;
                bool rightEdge  = p.X >= rect.Right - border;
                bool topEdge    = p.Y <= rect.Top   + border;
                bool bottomEdge = p.Y >= rect.Bottom - border;

                if (topEdge && leftEdge)    return (IntPtr)HTTOPLEFT;
                if (topEdge && rightEdge)   return (IntPtr)HTTOPRIGHT;
                if (bottomEdge && leftEdge) return (IntPtr)HTBOTTOMLEFT;
                if (bottomEdge && rightEdge)return (IntPtr)HTBOTTOMRIGHT;

                return (IntPtr)HTCLIENT;
            }

            // Fallback: original behavior based on window edges
            bool winLeft   = p.X <= border;
            bool winRight  = p.X >= ActualWidth  - border;
            bool winTop    = p.Y <= border;
            bool winBottom = p.Y >= ActualHeight - border;

            if (winTop && winLeft)     return (IntPtr)HTTOPLEFT;
            if (winTop && winRight)    return (IntPtr)HTTOPRIGHT;
            if (winBottom && winLeft)  return (IntPtr)HTBOTTOMLEFT;
            if (winBottom && winRight) return (IntPtr)HTBOTTOMRIGHT;

            return (IntPtr)HTCLIENT;
        }

        private void EnforceAspectRatioOnce()
        {
            if (_isAdjustingSize) return;

            _isAdjustingSize = true;
            try
            {
                // Choose the adjustment that changes the window the least.
                double currentW = Width;
                double currentH = Height;

                if (currentW <= 0 || currentH <= 0) return;

                double targetHFromW = currentW / _aspectRatio;
                double targetWFromH = currentH * _aspectRatio;

                // Clamp to mins
                if (targetHFromW < MinHeight) targetHFromW = MinHeight;
                if (targetWFromH < MinWidth)  targetWFromH = MinWidth;

                double deltaH = Math.Abs(targetHFromW - currentH);
                double deltaW = Math.Abs(targetWFromH - currentW);

                if (deltaH <= deltaW)
                {
                    Height = targetHFromW;
                    // keep width consistent with new height if mins forced us
                    Width = Math.Max(MinWidth, Height * _aspectRatio);
                }
                else
                {
                    Width = targetWFromH;
                    Height = Math.Max(MinHeight, Width / _aspectRatio);
                }
            }
            finally
            {
                _isAdjustingSize = false;
            }
            //Console.WriteLine($"[After Snap] Window = {ActualWidth} x {ActualHeight}, Content = {RootGrid.ActualWidth} x {RootGrid.ActualHeight}");
        }

        /// Update aspect ratio based on current content size
        private void UpdateAspectRatioFromContent()
        {
            if (RootGrid.ActualHeight <= 0) return;
            _aspectRatio = RootGrid.ActualWidth / RootGrid.ActualHeight;
            //Console.WriteLine($"[Aspect] from content = {_aspectRatio:0.000}");
        }

        /// Relearn aspect ratio and enforce it immediately
        public void RelearnAspectRatioFromCurrentWindow()
        {
            UpdateAspectRatioFromContent();
            _ignoreNextSizeChanged = true;
            EnforceAspectRatioOnce();
        }

        /// Indicates whether the JSON reader is running and both AGC and LGC files are available.
        public bool ReaderRunning => _readerRunning;

        private void InitializeJsonReader()
        {
            var (agcFile, lgcFile) = GetJsonFilePaths();

            bool agcExists = File.Exists(agcFile);
            bool lgcExists = lgcFile is not null && File.Exists(lgcFile);

            _readerRunning = agcExists && lgcExists;

            // Simple console logging so you can see startup status.
            Console.WriteLine("[DSKY-Bridge] Startup");
            Console.WriteLine($"AGC JSON: {(agcExists ? "OK" : "MISSING")}");
            Console.WriteLine($"LGC JSON: {(lgcExists ? "OK" : "MISSING")}");
            Console.WriteLine($"UDP Port: {(IsReentryPortOpen() ? "OPEN" : "CLOSED")} (127.0.0.1:8051)");
            Console.WriteLine($"Reader: {(_readerRunning ? "RUNNING" : "STOPPED")}");

            // Update the initial indicator state once at startup.
            UpdateLinkIndicators();

            // Start polling immediately; if files are missing now, the loop will
            // begin reading as soon as they appear.
            _pollTimer.Start();
        }

        private async Task PollOnceAsync()
        {
            var (agcFile, lgcFile) = GetJsonFilePaths();

            // Read both files in parallel; if either is missing, we keep the last values.
            Task<CMCValues?> agcTask = ReadAgcValuesIfExistsAsync(agcFile);
            Task<LGCValues?> lgcTask = lgcFile is not null
                ? ReadLgcValuesIfExistsAsync(lgcFile)
                : Task.FromResult<LGCValues?>(null);

            await Task.WhenAll(agcTask, lgcTask).ConfigureAwait(false);

            CMCValues? agcValues = agcTask.Result;
            LGCValues? lgcValues = lgcTask.Result;

            if (agcValues is not null && lgcValues is not null)
            {
                _lastAgcValues = agcValues;
                _lastLgcValues = lgcValues;
                _readerRunning = true;

                // Build and broadcast current DSKY state to api-dsky
                var state = BuildBridgeState();
                await _bridgeServer.BroadcastStatePerSlotAsync(slot => BuildBridgeStateForSlot(slot));
            }
            else
            {
                _readerRunning = false;
            }

            // We are likely on a background thread here due to ConfigureAwait(false),
            // so marshal the indicator update back onto the UI dispatcher.
            try
            {
                Dispatcher.Invoke(UpdateLinkIndicators);
            }
            catch
            {
                // Window might be closing; ignore dispatcher failures.
            }
        }

        // Fixed aspect ratio resizing
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_ignoreNextSizeChanged)
            {
                _ignoreNextSizeChanged = false;
                return;
            }

            if (_isAdjustingSize) return;

            // While the user is dragging the resize border, don't continuously
            // rewrite Width/Height (it causes the "jumping back" feeling).
            // We'll enforce the aspect ratio once on WM_EXITSIZEMOVE.
            if (_isInResizeMove) return;

            // Ignore if not fully initialized
            if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0) return;

            _isAdjustingSize = true;
            try
            {
                // Determine which dimension user likely changed more
                double dw = Math.Abs(e.NewSize.Width - e.PreviousSize.Width);
                double dh = Math.Abs(e.NewSize.Height - e.PreviousSize.Height);

                if (dw >= dh)
                {
                    // Width changed: derive height
                    double targetHeight = e.NewSize.Width / _aspectRatio;

                    // Respect MinHeight
                    if (targetHeight < MinHeight)
                    {
                        targetHeight = MinHeight;
                        Width = targetHeight * _aspectRatio;
                    }

                    Height = targetHeight;
                }
                else
                {
                    // Height changed: derive width
                    double targetWidth = e.NewSize.Height * _aspectRatio;

                    // Respect MinWidth
                    if (targetWidth < MinWidth)
                    {
                        targetWidth = MinWidth;
                        Height = targetWidth / _aspectRatio;
                    }

                    Width = targetWidth;
                }
            }
            finally
            {
                _isAdjustingSize = false;
            }
        }

        private static (string agcFile, string? lgcFile) GetJsonFilePaths()
        {
            string localLow = Path.GetFullPath(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow"));

            string exportDir = Path.Combine(localLow, "Wilhelmsen Studios", "ReEntry", "Export", "Apollo");

            string agcFile = Path.Combine(exportDir, "outputAGC.json");

            // Prefer dedicated LGC file if present; fall back to outputLM.json.
            string lgcFileA = Path.Combine(exportDir, "outputLGC.json");
            string lgcFileB = Path.Combine(exportDir, "outputLM.json");

            string? lgcFile = File.Exists(lgcFileA)
                ? lgcFileA
                : (File.Exists(lgcFileB) ? lgcFileB : null);

            return (agcFile, lgcFile);
        }

        private static async Task<CMCValues?> ReadAgcValuesIfExistsAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                return JsonSerializer.Deserialize<CMCValues>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<LGCValues?> ReadLgcValuesIfExistsAsync(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                return JsonSerializer.Deserialize<LGCValues>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// Update the advertised mDNS service to use the given IPv4 address.
        private void UpdateMdnsAdvertisedAddress(IPAddress? address)
        {
            if (_mdns == null)
                return;

            try
            {
                // Stop advertising previous profile if any
                if (_mdnsProfile != null)
                {
                    _mdns.Unadvertise(_mdnsProfile);
                    _mdnsProfile = null;
                }

                if (address == null)
                {
                    Console.WriteLine("[mDNS] No suitable IPv4 address found to advertise.");
                    return;
                }

                if (string.IsNullOrEmpty(_mdnsHostName))
                {
                    _mdnsHostName = System.Net.Dns.GetHostName();
                }

                if (string.IsNullOrEmpty(_mdnsInstanceName))
                {
                    _mdnsInstanceName = $"DSKY Bridge ({_mdnsHostName})";
                }

                var addresses = new[] { address };

                Console.WriteLine("[mDNS] Advertising IP: " + address);

                _mdnsProfile = new ServiceProfile(
                    _mdnsInstanceName,
                    MdnsServiceType,
                    (ushort)WebSocketPort,
                    addresses);

                _mdnsProfile.AddProperty("wsPath", "/ws");
                _mdnsProfile.AddProperty("version", "1.0.0");
                _mdnsProfile.AddProperty("mode", "bridge");
                _mdnsProfile.AddProperty("hostname", _mdnsHostName);

                _mdns.Advertise(_mdnsProfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mDNS] Failed to update mDNS advertising: {ex.Message}");
            }
        }

        /// Apply the user's choice of local IP from the overlay menu.
        private void ApplyLocalIpChoice(IpOption option)
        {
            if (option.Address.Equals(IPAddress.None))
                return; // "no IPs" placeholder

            _selectedLocalIp = option.Address;

            // Update the display in the main UI
            LocalIP.Content = FormatIpBlocks(option.Address.ToString());

            // Update mDNS advertisement to use the chosen IP
            UpdateMdnsAdvertisedAddress(option.Address);

            // Hide the menu
            Menu1.Visibility = Visibility.Collapsed;
        }

        /// Refresh the list of local IPv4 addresses for the overlay menu.
        private void RefreshLocalIpList()
        {
            _ipOptions.Clear();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties props;
                try
                {
                    props = ni.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (var ua in props.UnicastAddresses
                            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                {
                    _ipOptions.Add(new IpOption
                    {
                        AdapterName = ni.Name,
                        Address     = ua.Address
                    });
                }
            }

            // Get the ListBox instance dynamically
            var ipListBox = FindName("IpListBox") as ListBox;
            if (ipListBox == null)
            {
                // If the XAML name doesn't match, we just bail out silently
                return;
            }

            ipListBox.ItemsSource = null;
            ipListBox.ItemsSource = _ipOptions;

            if (_ipOptions.Count == 0)
            {
                // Optional: show a "no IPs" placeholder
                ipListBox.ItemsSource = new[]
                {
                    new IpOption
                    {
                        AdapterName = "No active IPv4 addresses found",
                        Address     = IPAddress.None
                    }
                };
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Stop polling to avoid any stray UI updates after close
            try { _pollTimer.Stop(); } catch { /* ignore */ }

            // Dispose UDP sender if applicable
            if (_commandSender is IDisposable d)
            {
                try { d.Dispose(); } catch { /* ignore */ }
            }

            // Stop advertising mDNS
            try
            {
                if (_mdnsProfile != null && _mdns != null)
                {
                    _mdns.Unadvertise(_mdnsProfile);
                }

                _mdns?.Dispose();
            }
            catch { /* ignore */ }

            try { _bridgeServer.Dispose(); } catch { /* ignore */ }

            base.OnClosed(e);
        }

        // Window drag movement
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            // Mouse position in window coordinates
            var p = e.GetPosition(this);
            double border = EffectiveResizeBorder;

            // If the cursor is on a resize corner, let Windows handle the resize.
            // (DragMove would otherwise "fight" the resize interaction.)
            if (RootGrid != null && RootGrid.IsLoaded)
            {
                var rect = RootGrid
                    .TransformToAncestor(this)
                    .TransformBounds(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight));

                bool leftEdge   = p.X <= rect.Left  + border;
                bool rightEdge  = p.X >= rect.Right - border;
                bool topEdge    = p.Y <= rect.Top   + border;
                bool bottomEdge = p.Y >= rect.Bottom - border;

                if ((topEdge && leftEdge) || (topEdge && rightEdge) ||
                    (bottomEdge && leftEdge) || (bottomEdge && rightEdge))
                {
                    return;
                }
            }
            else
            {
                bool leftEdge   = p.X <= border;
                bool rightEdge  = p.X >= ActualWidth  - border;
                bool topEdge    = p.Y <= border;
                bool bottomEdge = p.Y >= ActualHeight - border;

                if ((topEdge && leftEdge) || (topEdge && rightEdge) ||
                    (bottomEdge && leftEdge) || (bottomEdge && rightEdge))
                {
                    return;
                }
            }

            DragMove();
        }

        private object BuildBridgeStateForSlot(int slot)
        {
            bool isCm = GetIsInCommandModule(slot == 2 ? _dskyMode2 : _dskyMode);

            // --- CMC branch ---------------------------------------------------------
            if (isCm && _lastAgcValues is CMCValues agc)
            {
                var verbD1 = agc.HideVerb ? "" : agc.VerbD1;
                var verbD2 = agc.HideVerb ? "" : agc.VerbD2;
                var nounD1 = agc.HideNoun ? "" : agc.NounD1;
                var nounD2 = agc.HideNoun ? "" : agc.NounD2;

                // Same ranges we discussed earlier
                int display = NormalizeBrightness(agc.BrightnessNumerics, 0.2f, 1.14117646f);
                int status  = display;
                int keyboard = NormalizeBrightness(agc.BrightnessIntegral, 0.0f, 0.9411765f);

                return new
                {
                    agc.IsInCM,
                    IsInLM = false,

                    agc.ProgramD1,
                    agc.ProgramD2,
                    VerbD1 = verbD1,
                    VerbD2 = verbD2,
                    NounD1 = nounD1,
                    NounD2 = nounD2,

                    agc.Register1Sign,
                    agc.Register1D1,
                    agc.Register1D2,
                    agc.Register1D3,
                    agc.Register1D4,
                    agc.Register1D5,

                    agc.Register2Sign,
                    agc.Register2D1,
                    agc.Register2D2,
                    agc.Register2D3,
                    agc.Register2D4,
                    agc.Register2D5,

                    agc.Register3Sign,
                    agc.Register3D1,
                    agc.Register3D2,
                    agc.Register3D3,
                    agc.Register3D4,
                    agc.Register3D5,

                    agc.IlluminateCompLight,
                    agc.IlluminateUplinkActy,
                    agc.IlluminateNoAtt,
                    agc.IlluminateStby,
                    agc.IlluminateKeyRel,
                    agc.IlluminateOprErr,
                    agc.IlluminateTemp,
                    agc.IlluminateGimbalLock,
                    agc.IlluminateProg,
                    agc.IlluminateRestart,
                    agc.IlluminateTracker,

                    // LM-only lights not present in CMCValues
                    IlluminateNoDap   = 0,
                    IlluminatePrioDisp= 0,
                    IlluminateAlt     = 0,
                    IlluminateVel     = 0,

                    StatusBrightness  = status,
                    DisplayBrightness = display,
                    KeyboardBrightness= keyboard,

                    Standby = false
                };
            }

            // --- LMC branch ---------------------------------------------------------
            if (!isCm && _lastLgcValues is LGCValues lgc)
            {
                // that actually exist on that class. LM-specific lights are set to 0.

                var verbD1 = lgc.HideVerb ? "" : lgc.VerbD1;
                var verbD2 = lgc.HideVerb ? "" : lgc.VerbD2;
                var nounD1 = lgc.HideNoun ? "" : lgc.NounD1;
                var nounD2 = lgc.HideNoun ? "" : lgc.NounD2;

                // You can tweak ranges for LM later if Reentry uses different ones;
                // this will at least behave correctly for now.
                int display = NormalizeBrightness(lgc.BrightnessNumerics, 0.2f, 1.14117646f);
                int status  = display;
                int keyboard = NormalizeBrightness(lgc.BrightnessIntegral, 0.0f, 0.9411765f);

                return new
                {
                    IsInCM = false,
                    IsInLM = true,

                    lgc.ProgramD1,
                    lgc.ProgramD2,
                    VerbD1 = verbD1,
                    VerbD2 = verbD2,
                    NounD1 = nounD1,
                    NounD2 = nounD2,

                    lgc.Register1Sign,
                    lgc.Register1D1,
                    lgc.Register1D2,
                    lgc.Register1D3,
                    lgc.Register1D4,
                    lgc.Register1D5,

                    lgc.Register2Sign,
                    lgc.Register2D1,
                    lgc.Register2D2,
                    lgc.Register2D3,
                    lgc.Register2D4,
                    lgc.Register2D5,

                    lgc.Register3Sign,
                    lgc.Register3D1,
                    lgc.Register3D2,
                    lgc.Register3D3,
                    lgc.Register3D4,
                    lgc.Register3D5,

                    lgc.IlluminateCompLight,
                    lgc.IlluminateUplinkActy,
                    lgc.IlluminateNoAtt,
                    lgc.IlluminateStby,
                    lgc.IlluminateKeyRel,
                    lgc.IlluminateOprErr,
                    lgc.IlluminateTemp,
                    lgc.IlluminateGimbalLock,
                    lgc.IlluminateProg,
                    lgc.IlluminateRestart,
                    lgc.IlluminateTracker,

                    // LM-only lights not present on CMCValues → off
                    IlluminateNoDap   = 0,
                    IlluminatePrioDisp= 0,
                    IlluminateAlt      = lgc.IlluminateAlt,
                    IlluminateVel      = lgc.IlluminateVel,

                    StatusBrightness  = status,
                    DisplayBrightness = display,
                    KeyboardBrightness= keyboard,

                    Standby = false
                };
            }

            // --- Fallback: no data yet ----------------------------------------------
            return new
            {
                IlluminateCompLight = false,
                ProgramD1 = "",
                ProgramD2 = "",
                VerbD1 = "",
                VerbD2 = "",
                NounD1 = "",
                NounD2 = "",
                Register1Sign = "",
                Register1D1 = "",
                Register1D2 = "",
                Register1D3 = "",
                Register1D4 = "",
                Register1D5 = "",
                Register2Sign = "",
                Register2D1 = "",
                Register2D2 = "",
                Register2D3 = "",
                Register2D4 = "",
                Register2D5 = "",
                Register3Sign = "",
                Register3D1 = "",
                Register3D2 = "",
                Register3D3 = "",
                Register3D4 = "",
                Register3D5 = "",
                IlluminateUplinkActy = 0,
                IlluminateNoAtt = 0,
                IlluminateStby = 0,
                IlluminateKeyRel = 0,
                IlluminateOprErr = 0,
                IlluminateTemp = 0,
                IlluminateGimbalLock = 0,
                IlluminateProg = 0,
                IlluminateRestart = 0,
                IlluminateTracker = 0,
                IlluminateAlt = 0,
                IlluminateVel = 0,
                IlluminateNoDap = 0,
                IlluminatePrioDisp = 0,
                StatusBrightness = 127,
                DisplayBrightness = 127,
                KeyboardBrightness = 127,
                Standby = true
            };
        }

        // NEW: per-slot version of BuildBridgeState
        private object BuildBridgeState()
        {
            // Default to slot 1 (primary DSKY)
            return BuildBridgeStateForSlot(1);
        }

        //helper to determine if we're in CMC or LMC based on _dskyMode and json values
        // Helper: interpret a mode value (CMC/LMC/AUTO) into "is CM?"
        private bool GetIsInCommandModule(int mode)
        {
            return mode switch
            {
                CMC  => true,
                LMC  => false,
                AUTO => _lastAgcValues?.IsInCM ?? true, // prefer AGC if present
                _    => true
            };
        }

        // Existing parameterless version keeps working for your display logic
        private bool GetIsInCommandModule()
        {
            return GetIsInCommandModule(_dskyMode);
        }

        // NEW: per-client routing, based on which slot the IP belongs to
        // NEW: per-client routing, based on connection slot ("1" or "2")
        private bool GetIsInCommandModuleForClient(string clientSlotId)
        {
            return clientSlotId switch
            {
                "2" => GetIsInCommandModule(_dskyMode2), // second DSKY
                "1" => GetIsInCommandModule(_dskyMode),  // first DSKY
                _   => GetIsInCommandModule(_dskyMode)   // fallback
            };
        }

        //helper to normalize brightness values
        private static int NormalizeBrightness(
            float value,
            float originalMin,
            float originalMax,
            int targetMin = 1,
            int targetMax = 127)
        {
            if (originalMax <= originalMin)
                return targetMin;

            var normalized = (value - originalMin) / (originalMax - originalMin);
            var clamped = Math.Clamp(normalized, 0f, 1f);
            var scaled = clamped * (targetMax - targetMin) + targetMin;
            return (int)Math.Clamp(Math.Round(scaled), targetMin, targetMax);
        }

        private void HandleFunctionalButtonPress(object sender)
        {

            if (RootGrid is null)
                return;

            if (sender is not FrameworkElement fe)
                return;

            var transform = fe.TransformToAncestor(RootGrid);
            var pos = transform.Transform(new Point(0, 0));
        }

        private void SwUp_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode = CMC;
            SwArm.Source = new BitmapImage(new Uri(ImgBase + "switch_up.png"));
        }

        private void SwCtr_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode = AUTO;
            SwArm.Source = new BitmapImage(new Uri(ImgBase + "switch_ctr.png"));
        }

        private void SwDn_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode = LMC;
            SwArm.Source = new BitmapImage(new Uri(ImgBase + "switch_dn.png"));
        }

        private void SwUp2_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode2 = CMC;
            SwArm2.Source = new BitmapImage(new Uri(ImgBase + "switch_up.png"));
        }

        private void SwCtr2_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode2 = AUTO;
            SwArm2.Source = new BitmapImage(new Uri(ImgBase + "switch_ctr.png"));
        }

        private void SwDn2_Click(object sender, RoutedEventArgs e)
        {
            _dskyMode2 = LMC;
            SwArm2.Source = new BitmapImage(new Uri(ImgBase + "switch_dn.png"));
        }

        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            RefreshLocalIpList();
            Menu1.Visibility = Visibility.Visible;
        }

        private void IpListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Prefer the sender, which should already be the actual ListBox
            var ipListBox = sender as ListBox ?? FindName("IpListBox") as ListBox;
            if (ipListBox == null)
                return;

            if (ipListBox.SelectedItem is not IpOption option)
                return;

            ApplyLocalIpChoice(option);
        }

        private void FunctionalButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleFunctionalButtonPress(sender);
        }

        private void FunctionalButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            return;
        }

        private void FunctionalButton_MouseLeave(object sender, MouseEventArgs e)
        {
            return;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
