using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Folding;
using JsonTool.Core.Models;
using JsonTool.Highlighting;
using JsonTool.ViewModels;

namespace JsonTool.Views;
public partial class MainWindow : Window
{
    private FoldingManager? _foldingManager;
    private bool _isUpdatingFromViewModel;

    public MainWindow()
    {
        InitializeComponent();
        
        SetupEditor();
        SetupDataContextBinding();
    }

    private void SetupEditor()
    {
        JsonSyntaxHighlighter.ConfigureJsonEditor(JsonEditor);
        _foldingManager = JsonSyntaxHighlighter.SetupFolding(JsonEditor);
        JsonEditor.TextChanged += OnEditorTextChanged;
    }

    private void SetupDataContextBinding()
    {
        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                if (!string.IsNullOrEmpty(vm.JsonContent) && JsonEditor.Text != vm.JsonContent)
                {
                    _isUpdatingFromViewModel = true;
                    JsonEditor.Text = vm.JsonContent;
                    _isUpdatingFromViewModel = false;
                }
            }
        };
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel) return;

        if (DataContext is MainViewModel vm)
        {
            vm.JsonContent = JsonEditor.Text;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.JsonContent))
        {
            if (DataContext is MainViewModel vm && JsonEditor.Text != vm.JsonContent)
            {
                _isUpdatingFromViewModel = true;
                var caretOffset = JsonEditor.CaretOffset;
                var verticalOffset = JsonEditor.VerticalOffset;
                
                JsonEditor.Text = vm.JsonContent;
                if (caretOffset <= JsonEditor.Text.Length)
                {
                    JsonEditor.CaretOffset = caretOffset;
                }
                JsonEditor.ScrollToVerticalOffset(verticalOffset);
                
                _isUpdatingFromViewModel = false;
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.EditorFontSize))
        {
            if (DataContext is MainViewModel vm)
            {
                JsonEditor.FontSize = vm.EditorFontSize;
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsModified)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (vm.SaveFileCommand.CanExecute(null))
                    {
                        vm.SaveFileCommand.Execute(null);
                    }
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "JSON Schema Tool v1.0\n\n" +
            "A JSON Schema editor with:\n" +
            "• Syntax highlighting (AvalonEdit)\n" +
            "• Schema validation\n" +
            "• Property metadata editing\n" +
            "• Undo/Redo support\n" +
            "• Auto-save functionality\n" +
            "• Markdown export\n\n" +
            "Design Patterns Used:\n" +
            "• Strategy Pattern - Validation\n" +
            "• Command Pattern - Undo/Redo\n" +
            "• Observer Pattern - Auto-save\n" +
            "• Template Method - JSON Processing\n" +
            "• Flyweight Pattern - Memory Optimization",
            "About JSON Schema Tool",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (JsonEditor.CanUndo)
        {
            JsonEditor.Undo();
        }
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (JsonEditor.CanRedo)
        {
            JsonEditor.Redo();
        }
    }
}