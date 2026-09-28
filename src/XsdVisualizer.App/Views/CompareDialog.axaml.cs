using Avalonia.Controls;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

/// <summary>Escolha do Before e do After entre os Schema Sets abertos.</summary>
public partial class CompareDialog : Window
{
    public CompareDialog() => InitializeComponent();

    public CompareDialog(IReadOnlyList<SchemaSet> sets) : this()
    {
        var names = sets.Select(s => s.Name).ToList();
        BeforeBox.ItemsSource = names;
        AfterBox.ItemsSource = names;
        BeforeBox.SelectedIndex = 0;
        AfterBox.SelectedIndex = sets.Count > 1 ? 1 : 0;
        void Validate() => CompareButton.IsEnabled = BeforeBox.SelectedIndex != AfterBox.SelectedIndex;
        BeforeBox.SelectionChanged += (_, _) => Validate();
        AfterBox.SelectionChanged += (_, _) => Validate();
        Validate();
        CompareButton.Click += (_, _) => Close((sets[BeforeBox.SelectedIndex], sets[AfterBox.SelectedIndex]));
        CancelButton.Click += (_, _) => Close(null);
    }
}
