using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using JsonTool.Core.TemplateMethod;

namespace JsonTool.Views;
public partial class FlatViewWindow : Window
{
    private string _jsonContent = string.Empty;
    private SimpleFlatViewProcessor _processor;
    private List<FlatViewEntry> _allEntries = new();
    private FlatViewStatistics _statistics = new();

    public FlatViewWindow()
    {
        InitializeComponent();
        _processor = new SimpleFlatViewProcessor();
    }
    public void SetJsonContent(string json)
    {
        _jsonContent = json;
        ProcessAndDisplay();
    }
    public static void ShowFlatView(string json, Window? owner = null)
    {
        var window = new FlatViewWindow();
        if (owner != null)
        {
            window.Owner = owner;
        }
        window.SetJsonContent(json);
        window.Show();
    }
    public static void ShowFlatViewDialog(string json, Window? owner = null)
    {
        var window = new FlatViewWindow();
        if (owner != null)
        {
            window.Owner = owner;
        }
        window.SetJsonContent(json);
        window.ShowDialog();
    }

    private void ProcessAndDisplay()
    {
        if (string.IsNullOrWhiteSpace(_jsonContent))
        {
            OutputTextBox.Text = "No JSON content to display";
            return;
        }

        try
        {
            ConfigureProcessor();
            var result = _processor.ProcessJson(_jsonContent);

            if (!result.Success)
            {
                OutputTextBox.Text = $"Error: {result.ErrorMessage}";
                return;
            }
            if (result.TransformedData is SimpleFlatViewResult flatResult)
            {
                _allEntries = flatResult.Entries;
                _statistics = flatResult.Statistics;
                UpdateStatistics();
            }
            ApplyFilterAndDisplay();
            HeaderText.Text = $"Flat View Output ({_allEntries.Count} entries)";
        }
        catch (Exception ex)
        {
            OutputTextBox.Text = $"Error processing JSON: {ex.Message}";
        }
    }

    private void ConfigureProcessor()
    {
        var separatorItem = SeparatorComboBox.SelectedItem as ComboBoxItem;
        _processor.PathSeparator = separatorItem?.Content?.ToString() ?? ".";
        var arrayItem = ArrayFormatComboBox.SelectedItem as ComboBoxItem;
        var arrayFormat = arrayItem?.Content?.ToString() ?? "[0]";
        _processor.ArrayIndexFormat = arrayFormat switch
        {
            "[0]" => "[{0}]",
            ".0" => ".{0}",
            ":0" => ":{0}",
            _ => "[{0}]"
        };
        _processor.IncludeTypes = ShowTypesCheckBox.IsChecked ?? false;
        _processor.IncludeNulls = ShowNullsCheckBox.IsChecked ?? true;
    }

    private void ApplyFilterAndDisplay()
    {
        var filter = FilterTextBox.Text?.Trim() ?? "";
        var entries = _allEntries;
        if (!string.IsNullOrEmpty(filter))
        {
            entries = _allEntries
                .Where(e => e.Path.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                           e.FormattedValue.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        var sb = new StringBuilder();
        var includeTypes = ShowTypesCheckBox.IsChecked ?? false;

        foreach (var entry in entries)
        {
            var line = $"{entry.Path}{_processor.AssignmentOperator}{entry.FormattedValue}";
            
            if (includeTypes)
            {
                line += $"  // {entry.Type}";
            }

            sb.AppendLine(line);
        }

        OutputTextBox.Text = sb.ToString().TrimEnd();
        if (!string.IsNullOrEmpty(filter))
        {
            HeaderText.Text = $"Flat View Output ({entries.Count} of {_allEntries.Count} entries)";
        }
        else
        {
            HeaderText.Text = $"Flat View Output ({_allEntries.Count} entries)";
        }
    }

    private void UpdateStatistics()
    {
        TotalEntriesText.Text = _statistics.TotalEntries.ToString();
        StringCountText.Text = _statistics.StringCount.ToString();
        NumberCountText.Text = _statistics.NumberCount.ToString();
        MaxDepthText.Text = _statistics.MaxDepth.ToString();
    }

    #region Event Handlers

    private void SeparatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !string.IsNullOrEmpty(_jsonContent))
        {
            ProcessAndDisplay();
        }
    }

    private void ArrayFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !string.IsNullOrEmpty(_jsonContent))
        {
            ProcessAndDisplay();
        }
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && !string.IsNullOrEmpty(_jsonContent))
        {
            ProcessAndDisplay();
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded && _allEntries.Count > 0)
        {
            ApplyFilterAndDisplay();
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(OutputTextBox.Text);
            MessageBox.Show("Copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files|*.txt|All Files|*.*",
            Title = "Save Flat View",
            FileName = "flat_view.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, OutputTextBox.Text, Encoding.UTF8);
                MessageBox.Show($"Saved to: {dialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessAndDisplay();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}