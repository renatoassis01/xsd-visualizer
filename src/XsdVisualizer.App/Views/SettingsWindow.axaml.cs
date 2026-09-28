using Avalonia.Controls;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Views;

public partial class SettingsWindow : Window
{
    private static readonly ThemeChoice[] Themes = [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark];
    private static readonly string[] Languages = [LanguageChoice.System, LanguageChoice.Portuguese, LanguageChoice.English];

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(App app) : this()
    {
        ThemeBox.ItemsSource = new[] { Strings.ThemeSystem, Strings.ThemeLight, Strings.ThemeDark };
        ThemeBox.SelectedIndex = Array.IndexOf(Themes, app.Preferences.Theme);
        // Nomes dos idiomas no próprio idioma, como é costume.
        LanguageBox.ItemsSource = new[] { Strings.LanguageSystem, "Português (Brasil)", "English" };
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(Languages, app.Preferences.Language));

        ThemeBox.SelectionChanged += (_, _) => app.SetTheme(Themes[ThemeBox.SelectedIndex]);
        LanguageBox.SelectionChanged += (_, _) => app.SetLanguage(Languages[LanguageBox.SelectedIndex], this);
        CloseButton.Click += (_, _) => Close();
    }
}
