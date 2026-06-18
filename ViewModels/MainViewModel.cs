using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using UdpSimulator.Models;
using UdpSimulator.Services;

namespace UdpSimulator.ViewModels
{
    public enum LogFilter { All, TxOnly, RxOnly }

    public partial class MainViewModel : ObservableObject, IDisposable
    {
        // ── Services ──────────────────────────────────────────────────────────
        private readonly UdpService      _udp      = new();
        private readonly PcapService     _pcap     = new();
        private readonly AutoSendService _autoSend;
        private readonly TemplateService _templates = new();
        private readonly SettingsService _settings  = new();
        private readonly DispatcherTimer _statsTimer;

        // ── Config bindings ───────────────────────────────────────────────────
        [ObservableProperty] private string  localIp      = "0.0.0.0";
        [ObservableProperty] private int     localPort    = 12345;
        [ObservableProperty] private string  remoteIp     = "127.0.0.1";
        [ObservableProperty] private int     remotePort   = 12346;
        [ObservableProperty] private UdpMode selectedMode = UdpMode.Bidirectional;
        public IEnumerable<UdpMode> Modes => Enum.GetValues<UdpMode>();

        // ── Connection state ──────────────────────────────────────────────────
        [ObservableProperty] private bool   isConnected  = false;
        [ObservableProperty] private string connectLabel = "Connect";
        [ObservableProperty] private string statusText   = "Disconnected";

        // ── Send ──────────────────────────────────────────────────────────────
        [ObservableProperty] private string sendHex  = "DE AD BE EF";
        [ObservableProperty] private string sendText = "";
        [ObservableProperty] private int    burstCount = 1;

        // ── Auto-send ─────────────────────────────────────────────────────────
        [ObservableProperty] private bool              isAutoSending   = false;
        [ObservableProperty] private int               autoIntervalMs  = 1000;
        [ObservableProperty] private string            autoPayloadHex  = "55 44 50 53 49 4D 00 01";
        [ObservableProperty] private AutoPayloadMode   autoPayloadMode = AutoPayloadMode.Fixed;
        [ObservableProperty] private int               counterOffset   = 0;
        [ObservableProperty] private string            autoSendLabel   = "Start Auto-send";
        public IEnumerable<AutoPayloadMode> PayloadModes => Enum.GetValues<AutoPayloadMode>();

        // ── File send ─────────────────────────────────────────────────────────
        [ObservableProperty] private string sendFilePath = "";

        // ── Pcap ──────────────────────────────────────────────────────────────
        [ObservableProperty] private bool   isPcapActive = false;
        [ObservableProperty] private string pcapPath     = "capture.pcap";
        [ObservableProperty] private string pcapLabel    = "Start Capture";

        // ── Stats ─────────────────────────────────────────────────────────────
        [ObservableProperty] private string statsText = "";
        [ObservableProperty] private string rateText  = "";
        [ObservableProperty] private string rttText   = "";

        private ulong _prevTx, _prevRx;
        private DateTime _lastTxTime;

        // ── Templates ─────────────────────────────────────────────────────────
        [ObservableProperty] private string  templateName     = "";
        [ObservableProperty] private string? selectedTemplate = null;
        public ObservableCollection<string> TemplateNames { get; } = new();

        // ── Packet log ────────────────────────────────────────────────────────
        public ObservableCollection<PacketLogEntry> PacketLog { get; } = new();

        [ObservableProperty] private PacketLogEntry? selectedPacket;
        [ObservableProperty] private string          hexDetail           = "";
        [ObservableProperty] private bool            isLoggingEnabled    = true;
        [ObservableProperty] private string          loggingLabel        = "Pause Log";
        [ObservableProperty] private bool            isAutoScrollEnabled = true;
        [ObservableProperty] private string          autoScrollLabel     = "Scroll: ON";
        [ObservableProperty] private LogFilter       logFilter           = LogFilter.All;

        public IEnumerable<LogFilter> LogFilters => Enum.GetValues<LogFilter>();
        public ICollectionView FilteredLog { get; }

        // ── Ctor ──────────────────────────────────────────────────────────────
        public MainViewModel()
        {
            _autoSend = new AutoSendService(_udp, _pcap);

            _udp.PacketReceived  += OnPacketReceived;
            _autoSend.PacketSent += OnPacketReceived;

            FilteredLog        = CollectionViewSource.GetDefaultView(PacketLog);
            FilteredLog.Filter = o => o is PacketLogEntry e && PassesFilter(e);

            // Load settings
            var s = _settings.Load();
            LocalIp       = s.LocalIp;
            LocalPort     = s.LocalPort;
            RemoteIp      = s.RemoteIp;
            RemotePort    = s.RemotePort;
            SelectedMode  = s.Mode;
            BurstCount    = s.BurstCount;
            AutoIntervalMs = s.AutoIntervalMs;
            AutoPayloadHex = s.AutoPayloadHex;
            SendHex        = s.SendHex;

            // Load templates
            RefreshTemplateNames();

            // pkts/sec timer
            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statsTimer.Tick += OnStatsTick;
            _statsTimer.Start();
        }

        partial void OnLogFilterChanged(LogFilter value) => FilteredLog.Refresh();

        private bool PassesFilter(PacketLogEntry e) => LogFilter switch
        {
            LogFilter.TxOnly => e.Direction == "TX",
            LogFilter.RxOnly => e.Direction == "RX",
            _                => true,
        };

        // ── Stats tick ────────────────────────────────────────────────────────

        private void OnStatsTick(object? sender, EventArgs e)
        {
            ulong tx = _udp.TxCount, rx = _udp.RxCount;
            ulong txRate = tx - _prevTx;
            ulong rxRate = rx - _prevRx;
            _prevTx = tx; _prevRx = rx;
            RateText = $"TX: {txRate} p/s   RX: {rxRate} p/s";
        }

        // ── Connection ────────────────────────────────────────────────────────

        [RelayCommand]
        private void ToggleConnect()
        {
            if (IsConnected)
            {
                _autoSend.Stop();
                IsAutoSending = false;
                AutoSendLabel = "Start Auto-send";
                _udp.Stop();
                IsConnected  = false;
                ConnectLabel = "Connect";
                StatusText   = "Disconnected";
            }
            else
            {
                var cfg = MakeConfig();
                if (!_udp.Start(cfg))
                {
                    MessageBox.Show($"Failed to bind {LocalIp}:{LocalPort}",
                                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                IsConnected  = true;
                ConnectLabel = "Disconnect";
                StatusText   = $"Connected  {LocalIp}:{LocalPort}  →  {RemoteIp}:{RemotePort}";
            }
        }

        // ── Send ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void SendHexData()
        {
            if (!IsConnected) return;
            byte[]? data = ParseHex(SendHex);
            if (data == null || data.Length == 0)
            { MessageBox.Show("Invalid hex input."); return; }
            DoBurst(data);
        }

        [RelayCommand]
        private void SendTextData()
        {
            if (!IsConnected) return;
            if (string.IsNullOrEmpty(SendText)) return;
            DoBurst(System.Text.Encoding.UTF8.GetBytes(SendText));
        }

        [RelayCommand]
        private void BrowseSendFile()
        {
            var dlg = new OpenFileDialog { Title = "Select file to send" };
            if (dlg.ShowDialog() == true) SendFilePath = dlg.FileName;
        }

        [RelayCommand]
        private void SendFile()
        {
            if (!IsConnected) return;
            if (!File.Exists(SendFilePath))
            { MessageBox.Show("File not found: " + SendFilePath); return; }
            DoBurst(File.ReadAllBytes(SendFilePath));
        }

        // ── Auto-send ─────────────────────────────────────────────────────────

        [RelayCommand]
        private void ToggleAutoSend()
        {
            if (!IsConnected) return;
            if (IsAutoSending)
            {
                _autoSend.Stop();
                IsAutoSending = false;
                AutoSendLabel = "Start Auto-send";
            }
            else
            {
                byte[]? payload = ParseHex(AutoPayloadHex);
                if (payload == null || payload.Length == 0)
                { MessageBox.Show("Invalid payload hex."); return; }

                _autoSend.Payload       = payload;
                _autoSend.IntervalMs    = AutoIntervalMs;
                _autoSend.PayloadMode   = AutoPayloadMode;
                _autoSend.CounterOffset = CounterOffset;
                _autoSend.Start(MakeConfig());
                IsAutoSending = true;
                AutoSendLabel = "Stop Auto-send";
            }
        }

        // ── Templates ─────────────────────────────────────────────────────────

        [RelayCommand]
        private void SaveTemplate()
        {
            if (string.IsNullOrWhiteSpace(TemplateName))
            { MessageBox.Show("Template name cannot be empty."); return; }
            _templates.Save(TemplateName, AutoPayloadHex);
            RefreshTemplateNames();
            SelectedTemplate = TemplateName;
        }

        [RelayCommand]
        private void LoadTemplate()
        {
            if (SelectedTemplate == null) return;
            var t = _templates.Get(SelectedTemplate);
            if (t != null) AutoPayloadHex = t.HexPayload;
        }

        [RelayCommand]
        private void DeleteTemplate()
        {
            if (SelectedTemplate == null) return;
            _templates.Delete(SelectedTemplate);
            RefreshTemplateNames();
            SelectedTemplate = null;
        }

        private void RefreshTemplateNames()
        {
            TemplateNames.Clear();
            foreach (var t in _templates.Templates)
                TemplateNames.Add(t.Name);
        }

        // ── Pcap ──────────────────────────────────────────────────────────────

        [RelayCommand]
        private void BrowsePcapPath()
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Save pcap file",
                Filter     = "Pcap files (*.pcap)|*.pcap|All files (*.*)|*.*",
                DefaultExt = "pcap",
                FileName   = PcapPath,
            };
            if (dlg.ShowDialog() == true) PcapPath = dlg.FileName;
        }

        [RelayCommand]
        private void TogglePcap()
        {
            if (IsPcapActive)
            {
                _pcap.Close();
                IsPcapActive = false;
                PcapLabel    = "Start Capture";
            }
            else
            {
                if (!_pcap.Open(PcapPath))
                { MessageBox.Show("Cannot open: " + PcapPath); return; }
                IsPcapActive = true;
                PcapLabel    = "Stop Capture";
            }
        }

        // ── Log ───────────────────────────────────────────────────────────────

        [RelayCommand]
        private void ClearLog() => PacketLog.Clear();

        [RelayCommand]
        private void ToggleLogging()
        {
            IsLoggingEnabled = !IsLoggingEnabled;
            LoggingLabel     = IsLoggingEnabled ? "Pause Log" : "Resume Log";
        }

        [RelayCommand]
        private void ToggleAutoScroll()
        {
            IsAutoScrollEnabled = !IsAutoScrollEnabled;
            AutoScrollLabel     = IsAutoScrollEnabled ? "Scroll: ON" : "Scroll: OFF";
        }

        [RelayCommand]
        private void ResendPacket()
        {
            if (SelectedPacket == null || !IsConnected) return;
            DoBurst((byte[])SelectedPacket.Data.Clone());
        }

        [RelayCommand]
        private void CopyHex()
        {
            if (!string.IsNullOrEmpty(HexDetail))
                Clipboard.SetText(HexDetail);
        }

        [RelayCommand]
        private void ExportLog()
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Export log as pcap",
                Filter     = "Pcap files (*.pcap)|*.pcap",
                DefaultExt = "pcap",
                FileName   = "exported.pcap",
            };
            if (dlg.ShowDialog() != true) return;

            using var writer = new PcapService();
            if (!writer.Open(dlg.FileName))
            { MessageBox.Show("Cannot create file."); return; }
            foreach (var e in PacketLog) writer.Write(e);
            MessageBox.Show($"Exported {PacketLog.Count} packets to {dlg.FileName}");
        }

        partial void OnSelectedPacketChanged(PacketLogEntry? value)
        {
            if (value == null) { HexDetail = ""; return; }
            HexDetail = FormatHexDetail(value.Data);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DoBurst(byte[] data)
        {
            int count = Math.Max(1, BurstCount);
            for (int i = 0; i < count; i++)
                DoSend((byte[])data.Clone());
        }

        private void DoSend(byte[] data)
        {
            if (!_udp.Send(data))
            { MessageBox.Show("Send failed."); return; }

            _lastTxTime = DateTime.Now;

            var entry = new PacketLogEntry
            {
                Timestamp = _lastTxTime,
                Direction = "TX",
                Peer      = $"{RemoteIp}:{RemotePort}",
                Size      = data.Length,
                Data      = data,
            };
            _pcap.Write(entry);
            AddToLog(entry);
            UpdateStats();
        }

        private void OnPacketReceived(PacketLogEntry entry)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // RTT: measure time from last TX to this RX
                if (entry.Direction == "RX" && _lastTxTime != default)
                {
                    double rtt = (entry.Timestamp - _lastTxTime).TotalMilliseconds;
                    if (rtt >= 0 && rtt < 60_000)
                        RttText = $"RTT: {rtt:F1} ms";
                }
                AddToLog(entry);
                UpdateStats();
            });
        }

        private void AddToLog(PacketLogEntry entry)
        {
            if (!IsLoggingEnabled) return;
            PacketLog.Add(entry);
            while (PacketLog.Count > 2000) PacketLog.RemoveAt(0);
        }

        private void UpdateStats()
        {
            StatsText = $"TX: {_udp.TxCount} pkts / {_udp.TxBytes} bytes   " +
                        $"RX: {_udp.RxCount} pkts / {_udp.RxBytes} bytes";
        }

        private UdpConfig MakeConfig() => new()
        {
            LocalIp    = LocalIp,
            LocalPort  = LocalPort,
            RemoteIp   = RemoteIp,
            RemotePort = RemotePort,
            Mode       = SelectedMode,
        };

        private static byte[]? ParseHex(string hex)
        {
            var digits = new System.Text.StringBuilder();
            foreach (char c in hex)
                if (Uri.IsHexDigit(c)) digits.Append(c);
            if (digits.Length % 2 != 0) digits.Insert(0, '0');
            if (digits.Length == 0) return null;

            byte[] result = new byte[digits.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(digits.ToString(i * 2, 2), 16);
            return result;
        }

        private static string FormatHexDetail(byte[] data)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < data.Length; i += 16)
            {
                sb.Append($"{i:X4}  ");
                int count = Math.Min(16, data.Length - i);
                for (int j = 0; j < count; j++)
                {
                    sb.Append($"{data[i + j]:X2} ");
                    if (j == 7) sb.Append(' ');
                }
                for (int j = count; j < 16; j++) sb.Append("   ");
                sb.Append(" |");
                for (int j = 0; j < count; j++)
                {
                    char c = (char)data[i + j];
                    sb.Append(c >= 32 && c < 127 ? c : '.');
                }
                sb.AppendLine("|");
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            _statsTimer.Stop();
            _settings.Save(new AppSettings
            {
                LocalIp        = LocalIp,
                LocalPort      = LocalPort,
                RemoteIp       = RemoteIp,
                RemotePort     = RemotePort,
                Mode           = SelectedMode,
                BurstCount     = BurstCount,
                AutoIntervalMs = AutoIntervalMs,
                AutoPayloadHex = AutoPayloadHex,
                SendHex        = SendHex,
            });
            _autoSend.Dispose();
            _udp.Dispose();
            _pcap.Dispose();
        }
    }
}
