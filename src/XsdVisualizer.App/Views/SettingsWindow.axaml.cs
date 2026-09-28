using Avalonia.Controls;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Views;

public partial class SettingsWindow : Window
{
    private static readonly string[] Languages = [LanguageChoice.System, LanguageChoice.Portuguese, LanguageChoice.English];

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(App app) : this()
    {
        var options = XsdVisualizer.App.Themes.ThemeCatalog.All.Select(t => new XsdVisualizer.App.Themes.ThemeOption(t)).ToList();
        ThemeBox.ItemsSource = options;
        ThemeBox.SelectedIndex = Math.Max(0, options.FindIndex(o => o.Choice == app.Preferences.Theme));
        // Nomes dos idiomas no próprio idioma, como é costume.
        LanguageBox.ItemsSource = new[] { Strings.LanguageSystem, "Português (Brasil)", "English" };
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(Languages, app.Preferences.Language));

        ThemeBox.SelectionChanged += (_, _) =>
        {
            if (ThemeBox.SelectedItem is XsdVisualizer.App.Themes.ThemeOption option) app.SetTheme(option.Choice);
        };
        LanguageBox.SelectionChanged += (_, _) => app.SetLanguage(Languages[LanguageBox.SelectedIndex], this);
        CloseButton.Click += (_, _) => Close();
    }
}
