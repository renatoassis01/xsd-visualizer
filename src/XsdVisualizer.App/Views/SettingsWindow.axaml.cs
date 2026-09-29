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
        FillDiffOptions(app);
        // Nomes dos idiomas no próprio idioma, como é costume.
        LanguageBox.ItemsSource = new[] { Strings.LanguageSystem, "Português (Brasil)", "English" };
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(Languages, app.Preferences.Language));

        ThemeBox.SelectionChanged += (_, _) =>
        {
            if (ThemeBox.SelectedItem is not XsdVisualizer.App.Themes.ThemeOption option) return;
            app.SetTheme(option.Choice);
            FillDiffOptions(app); // as amostras mostram as cores sobre o tema escolhido
        };
        DiffBox.SelectionChanged += (_, _) =>
        {
            if (!_fillingDiff && DiffBox.SelectedItem is XsdVisualizer.App.Themes.DiffOption option) app.SetDiffColors(option.Choice);
        };
        LanguageBox.SelectionChanged += (_, _) => app.SetLanguage(Languages[LanguageBox.SelectedIndex], this);
        CloseButton.Click += (_, _) => Close();
    }

    private bool _fillingDiff;

    private void FillDiffOptions(App app)
    {
        var options = XsdVisualizer.App.Themes.DiffOption.For(XsdVisualizer.App.Themes.ThemeApplier.Theme, XsdVisualizer.App.Themes.ThemeApplier.IsDark);
        _fillingDiff = true;
        DiffBox.ItemsSource = options;
        DiffBox.SelectedIndex = Math.Max(0, options.ToList().FindIndex(o => o.Choice == app.Preferences.DiffColors));
        _fillingDiff = false;
    }
}
