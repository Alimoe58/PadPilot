using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PadPilot.Core.Mods;

/// <summary>
/// Receives mod commands as JSON over UDP. Listens on localhost only, so only programs on this PC
/// can reach it. Every packet is answered with a status JSON (or an error) to the sender.
/// </summary>
public sealed class UdpModServer : IDisposable
{
    public const int DefaultPort = 6970;

    // Without this, Windows reports "connection reset" on the next receive after a reply could not be delivered.
    private const int SioUdpConnReset = -1744830452;

    private UdpClient? _client;
    private CancellationTokenSource? _cts;

    /// <summary>Raised for every packet: the command (or null) and an error text (or null).</summary>
    public event Action<ModCommand?, string?>? Received;

    /// <summary>Builds the status reply (controller, battery …) as JSON; called on the network thread.</summary>
    public Func<string>? StatusJson { get; set; }

    public int Port { get; private set; }
    public bool IsRunning => _client != null;

    /// <summary>Starts listening. Returns an error text if the port is in use.</summary>
    public string? Start(int port)
    {
        Stop();
        try
        {
            var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            if (OperatingSystem.IsWindows())
                client.Client.IOControl(SioUdpConnReset, [0, 0, 0, 0], null);
            _client = client;
            _cts = new CancellationTokenSource();
            Port = port;
            _ = ReceiveLoop(client, _cts.Token);
            return null;
        }
        catch (SocketException ex)
        {
            return ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"port {port} is already in use"
                : ex.Message;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _client?.Dispose();
        _client = null;
        _cts = null;
    }

    private async Task ReceiveLoop(UdpClient client, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await client.ReceiveAsync(token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            string text = Encoding.UTF8.GetString(packet.Buffer);
            ModCommandParser.TryParse(text, out var command, out var error);
            Received?.Invoke(command, error);

            string reply = error != null
                ? $"{{\"ok\":false,\"error\":{System.Text.Json.JsonSerializer.Serialize(error)}}}"
                : StatusJson?.Invoke() ?? "{\"ok\":true}";
            try
            {
                await client.SendAsync(Encoding.UTF8.GetBytes(reply), packet.RemoteEndPoint, token);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // The sender may not wait for an answer; that's fine.
            }
        }
    }

    public void Dispose() => Stop();
}
