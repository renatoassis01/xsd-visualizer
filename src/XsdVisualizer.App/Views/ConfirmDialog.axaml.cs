using Avalonia.Controls;

namespace XsdVisualizer.App.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string title, string message, string confirm, string cancel) : this()
    {
        Title = title;
        Message.Text = message;
        ConfirmButton.Content = confirm;
        CancelButton.Content = cancel;
        ConfirmButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}
