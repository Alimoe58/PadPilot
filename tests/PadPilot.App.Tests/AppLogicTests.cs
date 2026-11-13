using PadPilot.App;
using PadPilot.Core;
using PadPilot.Core.Mapping;

namespace PadPilot.App.Tests;

public class AppLogicTests
{
    [Fact]
    public void EveryPresetIsRecognisedAsItself()
    {
        foreach (var preset in TriggerPresets.All.Where(p => p != TriggerPreset.Custom))
            Assert.Equal(preset, TriggerPresets.Match(TriggerPresets.Get(preset)!));
    }

    [Fact]
    public void ValuesAModeDoesNotUseDoNotBreakThePresetMatch()
    {
        var pistol = TriggerPresets.Get(TriggerPreset.Pistol)! with { Frequency = 55, Strength2 = 1, Period = 9 };
        Assert.Equal(TriggerPreset.Pistol, TriggerPresets.Match(pistol));
        Assert.Equal(TriggerPreset.Custom, TriggerPresets.Match(pistol with { Strength = 1 }));
    }

    [Fact]
    public void ProfilesFrom12KeepTheirBatteryLightbar()
    {
        var output = new OutputViewModel();
        output.Apply(new OutputProfile { LightbarShowsBattery = true });
        Assert.Equal(LightbarEffect.Battery, output.LightbarEffect);

        var saved = output.ToProfile();
        Assert.Equal(LightbarEffect.Battery, saved.LightbarEffect);
        Assert.True(saved.LightbarShowsBattery); // 1.2 still understands it
    }

    [Theory]
    [InlineData(0x4D, KeyModifiers.Ctrl | KeyModifiers.Shift, "Ctrl+Shift+M")]
    [InlineData(0x31, KeyModifiers.None, "1")]
    [InlineData(0x20, KeyModifiers.None, "Space")]
    [InlineData(0x1B, KeyModifiers.Alt, "Alt+Esc")]
    [InlineData(0xA0, KeyModifiers.None, "Shift")]
    public void KeyNamesAreReadable(int key, KeyModifiers modifiers, string expected) =>
        Assert.Equal(expected, KeyNames.Format(key, modifiers));

    [Fact]
    public void MappingRowsOnlyStoreRealChanges()
    {
        var service = new MappingService(new NoKeyboard(), new NoMouse());
        var vm = new MappingViewModel(service);
        var cross = vm.Rows.First(r => r.Button == Buttons.Cross);
        var circle = vm.Rows.First(r => r.Button == Buttons.Circle);

        cross.Kind = ActionKind.Key;               // no key chosen yet → nothing stored
        circle.Kind = ActionKind.Button;           // still points at itself → nothing stored
        Assert.Empty(service.Settings.Buttons);

        cross.SetKey(0x20, KeyModifiers.None);
        circle.Target = Buttons.Square;
        Assert.Equal(2, service.Settings.Buttons.Count);
        Assert.Equal(Buttons.Square, service.Settings.Buttons[Buttons.Circle].Target);

        vm.ResetAll();
        Assert.True(service.Settings.IsEmpty);
    }

    [Fact]
    public void CrashLogHidesTheUserFolder()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string text = CrashLog.Sanitize($@"at Foo() in {profile}\source\x.cs; user {Environment.UserName}");
        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", text);
        if (Environment.UserName.Length >= 3)
            Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoKeyboard : IKeyboardOutput
    {
        public void Key(int virtualKey, bool down) { }
    }

    private sealed class NoMouse : PadPilot.Core.Mouse.IMouseOutput
    {
        public void Move(int dx, int dy) { }
        public void Button(PadPilot.Core.Mouse.MouseButton button, bool down) { }
        public void Wheel(int delta) { }
    }
}
