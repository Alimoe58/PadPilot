using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace PadPilot.App;

public sealed record LanguageOption(string Code, string Name);

/// <summary>
/// UI texts from Resources/Strings*.resx (English is the default, German in Strings.de.resx).
/// Bindings go through the indexer, so changing <see cref="Language"/> updates the whole UI at once.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private static readonly ResourceManager Strings = new("PadPilot.App.Resources.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("de", "Deutsch"),
        new("en", "English"),
    ];

    private CultureInfo _culture;

    private Loc()
    {
        // English until the user picks another language in Settings (independent of the Windows language).
        _culture = CultureInfo.GetCultureInfo(AppSettings.Current.Language ?? "en");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Strings.GetString(key, _culture) ?? $"#{key}";

    public string Language
    {
        get => _culture.TwoLetterISOLanguageName;
        set
        {
            // The language ComboBox briefly pushes an empty value while the window is built;
            // only accept real choices so that doesn't count (and get saved) as a language switch.
            if (value == Language || Languages.All(l => l.Code != value))
                return;
            _culture = CultureInfo.GetCultureInfo(value);
            AppSettings.Current.Language = value;
            AppSettings.Current.Save();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        }
    }

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object[] args) =>
        string.Format(Instance._culture, Instance[key], args);
}

/// <summary>XAML shorthand: <c>Text="{local:Tr Tab_Inputs}"</c> binds to the translated text.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
