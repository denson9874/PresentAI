using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PresentAI;

public sealed class CastServer : IDisposable
{
    private readonly PresentationHub _hub;
    private readonly CancellationTokenSource _cts = new();
    private readonly TcpListener _listener;
    private Task? _serverTask;

    public CastServer(PresentationHub hub)
    {
        _hub = hub;
        Port = FindOpenPort();
        ReceiverUrl = $"http://{GetLocalIpAddress()}:{Port}/";
        _listener = new TcpListener(IPAddress.Any, Port);
    }

    public int Port { get; }
    public string ReceiverUrl { get; }

    public void Start()
    {
        if (_serverTask is not null)
        {
            return;
        }

        _listener.Start();
        _serverTask = Task.Run(ListenAsync);
    }

    public void OpenReceiver()
    {
        Process.Start(new ProcessStartInfo(ReceiverUrl) { UseShellExecute = true });
    }

    public void OpenChromeCastWindow()
    {
        var chrome = FindBrowserExecutable("chrome.exe") ?? FindBrowserExecutable("msedge.exe");
        if (chrome is null)
        {
            OpenReceiver();
            return;
        }

        Process.Start(new ProcessStartInfo(chrome, $"--new-window --enable-media-router \"{ReceiverUrl}\"")
        {
            UseShellExecute = false
        });
    }

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                continue;
            }

            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        try
        {
            var requestLine = await reader.ReadLineAsync(_cts.Token) ?? "";
            var path = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "/";

            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_cts.Token)))
            {
            }

            if (path.Equals("/state", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAsync(stream, JsonSerializer.Serialize(_hub.Snapshot()), "application/json");
                return;
            }

            await WriteAsync(stream, ReceiverHtml, "text/html; charset=utf-8");
        }
        catch
        {
        }
    }

    private static async Task WriteAsync(Stream stream, string body, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(header);
        await stream.WriteAsync(bytes);
    }

    private static int FindOpenPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string GetLocalIpAddress()
    {
        foreach (var address in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            {
                return address.ToString();
            }
        }

        return "127.0.0.1";
    }

    private static string? FindBrowserExecutable(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", fileName)
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private const string ReceiverHtml = """
<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PresentAI Receiver</title>
  <style>
    html, body { margin: 0; width: 100%; height: 100%; background: #101218; color: #f7f1df; font-family: Segoe UI, system-ui, sans-serif; overflow: hidden; }
    main { box-sizing: border-box; min-height: 100%; display: grid; align-content: center; gap: 4vh; padding: 7vw; background: radial-gradient(circle at 80% 20%, rgba(47, 125, 121, .35), transparent 32%), #101218; }
    h1 { margin: 0; max-width: 1100px; font-size: clamp(42px, 7vw, 108px); line-height: 1.02; letter-spacing: 0; }
    ul { margin: 0; padding-left: 1.3em; max-width: 980px; font-size: clamp(24px, 3vw, 44px); line-height: 1.35; }
    li { margin: .55em 0; }
    .meta { position: fixed; right: 32px; bottom: 24px; color: #9fb7ad; font-size: 18px; }
  </style>
</head>
<body>
  <main>
    <h1 id="title">Waiting for deck</h1>
    <ul id="bullets"></ul>
  </main>
  <div class="meta" id="meta"></div>
  <script>
    async function refresh() {
      const res = await fetch('/state', { cache: 'no-store' });
      const state = await res.json();
      const slides = state.deck?.slides || [];
      const slide = slides[state.slideIndex] || {};
      document.getElementById('title').textContent = slide.title || state.deck?.title || 'Waiting for deck';
      document.getElementById('bullets').innerHTML = (slide.bullets || []).map(b => `<li>${escapeHtml(b)}</li>`).join('');
      document.getElementById('meta').textContent = slides.length ? `${state.slideIndex + 1} / ${slides.length}` : '';
    }
    function escapeHtml(value) {
      return String(value).replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
    }
    refresh();
    setInterval(refresh, 700);
  </script>
</body>
</html>
""";

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }
}
