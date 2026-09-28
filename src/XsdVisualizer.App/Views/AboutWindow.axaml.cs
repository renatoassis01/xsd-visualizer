using System.Reflection;
using Avalonia.Controls;
using XsdVisualizer.App.Resources;

namespace XsdVisualizer.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        Version.Text = string.Format(Strings.AboutVersion, version.Split('+')[0]);
        CloseButton.Click += (_, _) => Close();
    }
}
