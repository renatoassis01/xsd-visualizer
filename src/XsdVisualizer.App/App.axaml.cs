using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XsdVisualizer.App.ViewModels;
using XsdVisualizer.App.Views;

namespace XsdVisualizer.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.Exit += (_, _) => viewModel.Dispose();
            // Caminhos na linha de comando abrem como se tivessem sido arrastados.
            if (desktop.Args is { Length: > 0 } args) _ = viewModel.OpenDroppedAsync(args);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow window })
            _ = window.ShowAboutAsync();
    }
}
