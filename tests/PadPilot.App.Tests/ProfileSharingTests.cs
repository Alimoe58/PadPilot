using PadPilot.App;
using PadPilot.Core;
using PadPilot.Core.Emulation;
using PadPilot.Core.Mapping;

namespace PadPilot.App.Tests;

/// <summary>Export / import of profile files. Works in a temporary folder, never in %AppData%.</summary>
public sealed class ProfileSharingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("padpilot-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ExportedProfileReadsBackUnchanged()
    {
        var profile = new Profile
        {
            Outputs = new OutputProfile
            {
                Lightbar = "#FF8000",
                LightbarEffect = LightbarEffect.Breathing,
                RightTrigger = new TriggerProfile { Mode = TriggerMode.Bow, Start = 1, End = 6, Strength = 6, Strength2 = 8 },
            },
            Mapping = new MappingSettings
            {
                Buttons = new() { [Buttons.Mute] = new ButtonAction { Kind = ActionKind.Special, Special = SpecialAction.ToggleMicMute } },
                LeftStick = StickMode.Wasd,
            },
            Games = ["eldenring.exe"],
        };
        string path = Path.Combine(_dir, "Elden Ring.json");

        ProfileStore.Export(path, profile);
        var back = ProfileStore.ReadShared(path);

        Assert.NotNull(back);
        Assert.Equal(profile.Outputs.RightTrigger, back.Outputs.RightTrigger);
        Assert.Equal(LightbarEffect.Breathing, back.Outputs.LightbarEffect);
        Assert.Equal(SpecialAction.ToggleMicMute, back.Mapping.Buttons[Buttons.Mute].Special);
        Assert.Equal(StickMode.Wasd, back.Mapping.LeftStick);
        Assert.Equal(["eldenring.exe"], back.Games);
    }

    [Theory]
    [InlineData("{ \"hello\": 1 }")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"Outputs\": ")]
    [InlineData("just text")]
    [InlineData("{ \"Outputs\": { \"MicLed\": \"Disco\" } }")]
    public void FilesThatAreNoProfilesAreRejected(string content)
    {
        Assert.Null(ProfileStore.ReadShared(Write("file.json", content)));
    }

    [Fact]
    public void HugeFilesAreRejected()
    {
        Assert.Null(ProfileStore.ReadShared(Write("big.json", "{ \"Outputs\": {}, \"x\": \"" + new string('a', 1_100_000) + "\" }")));
    }

    [Fact]
    public void BrokenOrHostileValuesAreCleanedUp()
    {
        var p = ProfileStore.ReadShared(Write("weird.json", """
        {
          "Outputs": { "Lightbar": null, "LeftTrigger": null, "MicLed": 99, "LightbarEffect": 12, "EffectSpeed": 500 },
          "LeftStick": null,
          "EmulationMode": 42,
          "TouchpadMouse": { "Sensitivity": 1e9 },
          "GyroMouse": { "PixelsPerDegree": -5, "Bias": { "X": 300, "Y": 0, "Z": 0 } },
          "Mapping": { "Buttons": { "Cross": { "Kind": "Key", "Key": 70000 }, "Circle": { "Kind": "Key", "Key": 32 } }, "StickMouseSpeed": 1e12 },
          "Games": [ "game.exe", "GAME.exe", "..\\..\\evil.exe", "notes.txt", null ]
        }
        """));

        Assert.NotNull(p);
        Assert.NotNull(p.Outputs.Lightbar);
        Assert.NotNull(p.Outputs.LeftTrigger);
        Assert.NotNull(p.LeftStick);
        Assert.Equal(MicLed.Off, p.Outputs.MicLed);
        Assert.Equal(LightbarEffect.Static, p.Outputs.LightbarEffect);
        Assert.Equal(10, p.Outputs.EffectSpeed);
        Assert.Equal(EmulationMode.Off, p.EmulationMode);
        Assert.Equal(2, p.TouchpadMouse.Sensitivity);
        Assert.Equal(0.2, p.GyroMouse.PixelsPerDegree);
        Assert.Equal(default, p.GyroMouse.Bias);
        Assert.False(p.Mapping.Buttons.ContainsKey(Buttons.Cross));
        Assert.True(p.Mapping.Buttons.ContainsKey(Buttons.Circle));
        Assert.Equal(3000, p.Mapping.StickMouseSpeed);
        Assert.Equal(["game.exe"], p.Games);
    }

    [Theory]
    [InlineData("Elden Ring", new[] { "Standard" }, "Elden Ring")]
    [InlineData("Standard", new[] { "standard" }, "Standard (2)")]
    [InlineData("Standard", new[] { "Standard", "Standard (2)" }, "Standard (3)")]
    public void UniqueNames(string name, string[] taken, string expected) =>
        Assert.Equal(expected, ProfileStore.UniqueName(name, taken));

    [Theory]
    [InlineData("  Racing  ", "Racing")]
    [InlineData("a/b\\c:d", "abcd")]
    [InlineData("...", null)]
    [InlineData("", null)]
    public void CleanNames(string input, string? expected) => Assert.Equal(expected, ProfileStore.CleanName(input));
}
