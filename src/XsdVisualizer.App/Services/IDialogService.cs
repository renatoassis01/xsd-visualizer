namespace XsdVisualizer.App.Services;

/// <summary>Diálogos do sistema; implementado pela janela principal.</summary>
public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);
    Task<string?> PickXmlFileAsync(string title);
    Task<string?> PickSavePathAsync(string suggestedName);
    Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel);
    Task<(XsdVisualizer.Core.SchemaSet Before, XsdVisualizer.Core.SchemaSet After)?> PickComparisonAsync(IReadOnlyList<XsdVisualizer.Core.SchemaSet> sets);
    void ShowComparison(ViewModels.ComparisonViewModel comparison);
}
