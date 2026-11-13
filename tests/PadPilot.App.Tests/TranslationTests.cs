using System.Runtime.CompilerServices;
using System.Xml.Linq;
using PadPilot.App;
using PadPilot.Core;
using PadPilot.Core.Emulation;
using PadPilot.Core.Mapping;
using PadPilot.Core.Mouse;
using PadPilot.Core.Power;

namespace PadPilot.App.Tests;

/// <summary>Both languages have the same texts, and every option shown in a list has a label.</summary>
public class TranslationTests
{
    private static string ResourceFolder([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PadPilot.App", "Resources"));

    private static Dictionary<string, string> Read(string file) =>
        XDocument.Load(Path.Combine(ResourceFolder(), file)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static readonly Dictionary<string, string> English = Read("Strings.resx");
    private static readonly Dictionary<string, string> German = Read("Strings.de.resx");

    [Fact]
    public void BothLanguagesHaveTheSameKeys()
    {
        Assert.Empty(English.Keys.Except(German.Keys).Order());
        Assert.Empty(German.Keys.Except(English.Keys).Order());
    }

    [Fact]
    public void NoTextIsEmpty()
    {
        Assert.Empty(English.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key));
        Assert.Empty(German.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key));
    }

    [Fact]
    public void PlaceholdersMatchBetweenLanguages()
    {
        static string Holes(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"\{\d\}").Select(m => m.Value).Order());
        var mismatched = English.Keys.Intersect(German.Keys).Where(k => Holes(English[k]) != Holes(German[k]));
        Assert.Empty(mismatched);
    }

    public static IEnumerable<object[]> ListedEnums() =>
    [
        [typeof(TriggerMode)], [typeof(TriggerPreset)], [typeof(LightbarEffect)], [typeof(ActionKind)], [typeof(MouseAction)],
        [typeof(SpecialAction)], [typeof(StickMode)], [typeof(IdleTimeout)], [typeof(EmulationMode)], [typeof(MicLed)],
        [typeof(LedBrightness)], [typeof(GyroActivation)],
    ];

    /// <summary>Lists show enum values through "{Type}_{Value}" texts; a missing one would show the raw name.</summary>
    [Theory]
    [MemberData(nameof(ListedEnums))]
    public void EveryListedOptionHasALabel(Type type)
    {
        var missing = Enum.GetNames(type).Select(n => $"{type.Name}_{n}").Where(k => !English.ContainsKey(k));
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryMappableButtonHasALabel()
    {
        var missing = MappingViewModel.Targets.Append(Buttons.Mute).Select(b => $"Buttons_{b}").Where(k => !English.ContainsKey(k));
        Assert.Empty(missing);
    }
}
