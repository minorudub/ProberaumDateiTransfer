using Microsoft.Win32;
using QRCoder;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace minoru.SimpleDataTransfer;

public class MainWindowViewModel : ViewModelBase
{
    private LocalFileServer? _server;
    private string _folderPath = "";
    private string _port = "5000";
    private string _serverUrl = "";
    private string _statusMessage = "";
    private BitmapSource? _qrCodeImage;
    private bool _isServerStopped = true;
    public bool IsServerRunning => !IsServerStopped;

    public string FolderPath
    {
        get => _folderPath;
        set { _folderPath = value; OnPropertyChanged(); }
    }

    public string Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set { _serverUrl = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public BitmapSource? QrCodeImage
    {
        get => _qrCodeImage;
        set { _qrCodeImage = value; OnPropertyChanged(); }
    }

    public bool IsServerStopped
    {
        get => _isServerStopped;
        set { _isServerStopped = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsServerRunning)); }
    }

    public ICommand ChooseFolderCommand { get; }
    public ICommand StartServerCommand { get; }
    public ICommand StopServerCommand { get; }
    public ICommand ShowHelpCommand { get; }
    public ICommand CopyUrlCommand { get; }
    public ICommand OpenBrowserCommand { get; }
    public ICommand WindowClosingCommand { get; }

    public MainWindowViewModel()
    {
        var defaultFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "ProberaumTransfer");
        Directory.CreateDirectory(defaultFolder);
        FolderPath = defaultFolder;

        StatusMessage = "Ablauf: Smartphone-Hotspot an → PC verbindet sich → Start drücken → URL im Handy öffnen.";

        ChooseFolderCommand = new RelayCommand(ChooseFolder);
        StartServerCommand = new RelayCommand(async _ => await StartServer(), _ => IsServerStopped);
        StopServerCommand = new RelayCommand(async _ => await StopServer(), _ => !IsServerStopped);
        ShowHelpCommand = new RelayCommand(ShowHelp);
        CopyUrlCommand = new RelayCommand(CopyUrl, _ => !string.IsNullOrWhiteSpace(ServerUrl));
        OpenBrowserCommand = new RelayCommand(OpenBrowser, _ => !string.IsNullOrWhiteSpace(ServerUrl));
        WindowClosingCommand = new RelayCommand(async _ => await OnWindowClosing());
    }

    private void ChooseFolder(object? obj)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Ordner auswählen (Datei im Zielordner anklicken)",
            CheckFileExists = false,
            FileName = "Ordner auswählen"
        };

        if (dlg.ShowDialog() == true)
        {
            var folder = Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrWhiteSpace(folder))
                FolderPath = folder;
        }
    }

    private async Task StartServer()
    {
        if (!int.TryParse(Port, out var portNumber) || portNumber is < 1 or > 65535)
        {
            MessageBox.Show("Port ungültig (1-65535).");
            return;
        }

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            MessageBox.Show("Bitte einen gültigen Ordner wählen.");
            return;
        }

        try
        {
            _server = new LocalFileServer(FolderPath, portNumber);
            await _server.StartAsync();

            IsServerStopped = false;

            var ip = GetBestLocalIPv4() ?? "127.0.0.1";
            ServerUrl = $"http://{ip}:{portNumber}/";
            StatusMessage = "Server läuft. Smartphone und PC müssen im selben WLAN sein (z. B. Smartphone-Hotspot oder X-Air).";
            UpdateQr(ServerUrl);
        }
        catch (Exception ex)
        {
            _server = null;
            MessageBox.Show($"Start fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task StopServer()
    {
        if (_server == null) return;

        try
        {
            await _server.StopAsync();
            ServerUrl = "";
            UpdateQr(null);
        }
        finally
        {
            _server = null;
            IsServerStopped = true;
            StatusMessage = "Server gestoppt.";
        }
    }

    private void ShowHelp(object? obj)
    {
        var text =
        @"Proberaum Transfer – Kurzanleitung

        1) Smartphone-Hotspot aktivieren
        2) PC mit diesem WLAN verbinden
        3) Ordner auswählen (z.B. Aufnahmen)
        4) Start klicken
        5) QR-Code mit dem Smartphone scannen

        Download:
        → Datei anklicken

        Upload:
        → Datei auswählen und hochladen

        Wichtig:
        PC und Smartphone müssen im selben WLAN sein.";

        MessageBox.Show(text, "Anleitung", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyUrl(object? obj)
    {
        if (!string.IsNullOrWhiteSpace(ServerUrl))
            Clipboard.SetText(ServerUrl);
    }

    private void OpenBrowser(object? obj)
    {
        if (string.IsNullOrWhiteSpace(ServerUrl)) return;
        Process.Start(new ProcessStartInfo(ServerUrl) { UseShellExecute = true });
    }

    private async Task OnWindowClosing()
    {
        if (_server != null)
        {
            await StopServer();
        }
    }

    private void UpdateQr(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            QrCodeImage = null;
            return;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        byte[] pngBytes = qr.GetGraphic(pixelsPerModule: 10);
        QrCodeImage = PngBytesToBitmapSource(pngBytes);
    }

    private static BitmapSource PngBytesToBitmapSource(byte[] pngBytes)
    {
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(pngBytes);
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static string? GetBestLocalIPv4()
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(ni => new
            {
                ni,
                props = ni.GetIPProperties(),
                addrs = ni.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address)
                    .ToList()
            })
            .Where(x => x.addrs.Count > 0)
            .ToList();

        // 1) Prefer WLAN with a default gateway
        var wifi = nics
            .Where(x => x.ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .OrderByDescending(x => x.props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .FirstOrDefault();

        if (wifi != null)
            return PickBestIPv4(wifi.addrs);

        // 2) Otherwise: any NIC with a default gateway
        var gw = nics
            .OrderByDescending(x => x.props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .FirstOrDefault();

        if (gw != null)
            return PickBestIPv4(gw.addrs);

        return null;

        static string? PickBestIPv4(List<IPAddress> ips)
        {
            foreach (var ip in ips)
            {
                var b = ip.GetAddressBytes();
                // skip APIPA
                if (b[0] == 169 && b[1] == 254) continue;
                return ip.ToString();
            }
            return ips.FirstOrDefault()?.ToString();
        }
    }
}