using System.Net;
using System.Net.Sockets;
using System.Text;
using PadPilot.Core;
using PadPilot.Core.Mods;

namespace PadPilot.Core.Tests;

public class ModTests
{
    private static ModCommand Parse(string json)
    {
        Assert.True(ModCommandParser.TryParse(json, out var cmd, out var error), error);
        return cmd!;
    }

    [Fact]
    public void ParsesFullCommand()
    {
        var cmd = Parse("""
            {
              "lightbar": "#FF8000",
              "playerLeds": [1, 0, 1, 0, 1],
              "micLed": "pulse",
              "rumble": { "left": 200, "right": 50 },
              "leftTrigger": { "mode": "resistance", "start": 7, "strength": 8 },
              "rightTrigger": { "mode": "weapon", "start": 2, "end": 5, "strength": 8 }
            }
            """);

        Assert.Equal(((byte)255, (byte)128, (byte)0), cmd.Lightbar);
        Assert.Equal(PlayerLeds.Led1 | PlayerLeds.Led3 | PlayerLeds.Led5, cmd.PlayerLeds);
        Assert.Equal(MicLed.Pulse, cmd.MicLed);
        Assert.Equal(((byte)200, (byte)50), cmd.Rumble);
        Assert.Equal(TriggerEffect.Resistance(7, 8).Bytes.ToArray(), cmd.LeftTrigger!.Bytes.ToArray());
        Assert.Equal(TriggerEffect.Weapon(2, 5, 8).Bytes.ToArray(), cmd.RightTrigger!.Bytes.ToArray());
        Assert.False(cmd.IsStatusRequest);
    }

    [Fact]
    public void AcceptsAlternativeFormsAndAnyCase()
    {
        var cmd = Parse("""{ "LightBar": { "r": 1, "g": 2, "b": 300 }, "playerLeds": 4, "leftTrigger": "off" }""");

        Assert.Equal(((byte)1, (byte)2, (byte)255), cmd.Lightbar);
        Assert.Equal(PlayerLeds.Led3, cmd.PlayerLeds);
        Assert.Same(TriggerEffect.Off, cmd.LeftTrigger);
    }

    [Fact]
    public void EmptyObjectIsStatusRequest() => Assert.True(Parse("{}").IsStatusRequest);

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{ "colour": "#FF0000" }""")]
    [InlineData("""{ "rightTrigger": { "mode": "explode" } }""")]
    [InlineData("""{ "lightbar": "#12" }""")]
    public void RejectsInvalidCommands(string json)
    {
        Assert.False(ModCommandParser.TryParse(json, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void OverridesLayOverUserSettingsUntilReset()
    {
        var user = new OutputState { LightbarBlue = 255, RumbleLeft = 10, PlayerLeds = PlayerLeds.Led3 };
        var mods = new ModOverrides();

        mods.Apply(Parse("""{ "lightbar": "#FF0000", "rumble": { "left": 100, "right": 0 } }"""));
        var result = mods.ApplyTo(user);
        Assert.Equal((255, 0, 0), ((int)result.LightbarRed, (int)result.LightbarGreen, (int)result.LightbarBlue));
        Assert.Equal(100, result.RumbleLeft);
        Assert.Equal(PlayerLeds.Led3, result.PlayerLeds); // not touched by the mod
        Assert.True(mods.IsActive);

        mods.Apply(Parse("""{ "reset": true, "micLed": "on" }"""));
        result = mods.ApplyTo(user);
        Assert.Equal(255, result.LightbarBlue);
        Assert.Equal(10, result.RumbleLeft);
        Assert.Equal(MicLed.On, result.MicLed);
    }

    [Fact]
    public async Task UdpServerAppliesCommandAndReplies()
    {
        using var server = new UdpModServer { StatusJson = () => "{\"ok\":true,\"test\":1}" };
        int port = FreePort();
        Assert.Null(server.Start(port));

        var received = new TaskCompletionSource<ModCommand?>();
        server.Received += (cmd, _) => received.TrySetResult(cmd);

        using var client = new UdpClient(0, AddressFamily.InterNetwork);
        byte[] payload = Encoding.UTF8.GetBytes("""{ "lightbar": "#00FF00" }""");
        await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));

        var reply = await client.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var cmd = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(((byte)0, (byte)255, (byte)0), cmd!.Lightbar);
        Assert.Equal("{\"ok\":true,\"test\":1}", Encoding.UTF8.GetString(reply.Buffer));
    }

    [Fact]
    public async Task UdpServerRepliesWithErrorForInvalidJson()
    {
        using var server = new UdpModServer();
        int port = FreePort();
        Assert.Null(server.Start(port));

        using var client = new UdpClient(0, AddressFamily.InterNetwork);
        byte[] payload = Encoding.UTF8.GetBytes("hello");
        await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));

        var reply = await client.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.StartsWith("{\"ok\":false,\"error\":", Encoding.UTF8.GetString(reply.Buffer));
    }

    [Fact]
    public async Task FileWatcherReadsCommandsWrittenToTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"padpilot-test-{Guid.NewGuid():N}", "mod.json");
        using var watcher = new ModFileWatcher();
        var received = new TaskCompletionSource<ModCommand?>();
        watcher.Received += (cmd, _) => received.TrySetResult(cmd);
        watcher.Start(path);
        try
        {
            await File.WriteAllTextAsync(path, """{ "micLed": "on" }""");
            var cmd = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(MicLed.On, cmd!.MicLed);
        }
        finally
        {
            watcher.Stop();
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    private static int FreePort()
    {
        using var probe = new UdpClient(0, AddressFamily.InterNetwork);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }
}
