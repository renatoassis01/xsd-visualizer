using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
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
        ApplyLanguage(_session.Current.Language);
        ApplyTheme(_session.Current.Theme);
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

    private void ApplyTheme(ThemeChoice theme) => RequestedThemeVariant = theme switch
    {
        ThemeChoice.Light => ThemeVariant.Light,
        ThemeChoice.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default, // segue o sistema, inclusive quando ele muda
    };

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

    /// <summary>Menu do aplicativo no macOS (substitui o "About Avalonia"): Sobre e Configurações (⌘,).</summary>
    private void BuildNativeMenu()
    {
        var about = new NativeMenuItem(Strings.About);
        about.Click += (_, _) => { if (MainWindow is { } w) _ = w.ShowAboutAsync(); };
        var settings = new NativeMenuItem(Strings.SettingsMenu) { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
        settings.Click += (_, _) => ShowSettings();
        NativeMenu.SetMenu(this, new NativeMenu { Items = { about, new NativeMenuItemSeparator(), settings } });
    }
}
