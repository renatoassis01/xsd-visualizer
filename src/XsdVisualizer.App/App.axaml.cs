using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;
using XsdVisualizer.App.ViewModels;
using XsdVisualizer.App.Views;

namespace XsdVisualizer.App;

public partial class App : Application
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;
    private readonly SessionStore _session = new();
    private MainViewModel? _viewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // "Igual ao sistema": quando o sistema troca claro/escuro, editor e diffs acompanham.
        ActualThemeVariantChanged += (_, _) => Themes.ThemeApplier.ApplyResources(this);
        ApplyLanguage(_session.Current.Language);
        ApplyTheme(_session.Current.Theme);
        Themes.ThemeApplier.SetDiff(this, Themes.DiffPaletteCatalog.Get(_session.Current.DiffColors));
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = new MainViewModel(_session);
            desktop.MainWindow = new MainWindow { DataContext = _viewModel };
            desktop.Exit += (_, _) => _viewModel.Dispose();
            BuildNativeMenu();
            // Caminhos na linha de comando abrem como se tivessem sido arrastados.
            if (desktop.Args is { Length: > 0 } args) _ = _viewModel.OpenDroppedAsync(args);
        }
        base.OnFrameworkInitializationCompleted();
    }

    public Session Preferences => _session.Current;

    private MainWindow? MainWindow =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    public void ShowSettings()
    {
        if (MainWindow is { } owner) _ = new SettingsWindow(this).ShowDialog(owner);
    }

    public void SetTheme(ThemeChoice theme)
    {
        _session.Current.Theme = theme;
        _session.Save();
        ApplyTheme(theme);
    }

    public void SetDiffColors(DiffColorsChoice diff)
    {
        _session.Current.DiffColors = diff;
        _session.Save();
        Themes.ThemeApplier.SetDiff(this, Themes.DiffPaletteCatalog.Get(diff));
    }

    /// <summary>Troca o idioma na hora: recria a janela principal (mesmo view model) e reabre as Configurações nela.</summary>
    public void SetLanguage(string language, Window? settingsWindow)
    {
        if (_session.Current.Language == language) return;
        _session.Current.Language = language;
        _session.Save();
        ApplyLanguage(language);
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || _viewModel is null) return;

        var old = desktop.MainWindow;
        var window = new MainWindow { DataContext = _viewModel };
        if (old is not null)
        {
            window.Width = old.Width;
            window.Height = old.Height;
            window.Position = old.Position;
            window.WindowState = old.WindowState;
        }
        desktop.MainWindow = window;
        window.Show();
        settingsWindow?.Close();
        old?.Close();
        _viewModel.RefreshTexts();
        BuildNativeMenu();
        if (settingsWindow is not null) ShowSettings();
    }

    private void ApplyTheme(ThemeChoice theme) => Themes.ThemeApplier.Apply(this, Themes.ThemeCatalog.Get(theme));

    /// <summary>"system" usa português se o sistema estiver em português; senão, inglês.</summary>
    private static void ApplyLanguage(string language)
    {
        var culture = language switch
        {
            LanguageChoice.Portuguese => new CultureInfo("pt-BR"),
            LanguageChoice.English => new CultureInfo("en"),
            _ => SystemCulture.TwoLetterISOLanguageName == "pt" ? SystemCulture : new CultureInfo("en"),
        };
        Strings.Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>Textos do menu do aplicativo no macOS (declarado em App.axaml) no idioma atual.</summary>
    private void BuildNativeMenu()
    {
        if (NativeMenu.GetMenu(this) is not { } menu) return;
        var items = menu.Items.OfType<NativeMenuItem>().Where(i => i is not NativeMenuItemSeparator).ToList();
        items[0].Header = Strings.About;
        items[1].Header = Strings.SettingsMenu;
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        if (MainWindow is { } window) _ = window.ShowAboutAsync();
    }

    private void OnSettingsClick(object? sender, EventArgs e) => ShowSettings();
}
