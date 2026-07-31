using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PwaDrop.WpfDropTarget;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        application.Run(new DropTargetWindow());
    }
}

internal sealed class DropTargetWindow : Window
{
    private readonly ListBox _receivedFiles;
    private readonly Border _dropZone;

    internal DropTargetWindow()
    {
        Title = "PWADrop WPF target";
        Width = 760;
        Height = 520;
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0B1123");
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Text");

        var root = new Grid { Margin = new Thickness(36) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(210) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = ".NET WPF drop target",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "A plain WPF target that knows only the standard Windows FileDrop format.",
            Foreground = Brush("#B5BCCF"),
            FontSize = 15,
            Margin = new Thickness(2, 8, 0, 0)
        });
        Grid.SetRow(header, 0);

        _dropZone = new Border
        {
            AllowDrop = true,
            Background = Brush("#111A31"),
            BorderBrush = Brush("#4A6AFF"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Child = new TextBlock
            {
                Text = "Drop files here",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _dropZone.DragEnter += HandleDragEnter;
        _dropZone.DragOver += HandleDragEnter;
        _dropZone.DragLeave += (_, _) => _dropZone.Background = Brush("#111A31");
        _dropZone.Drop += HandleDrop;
        Grid.SetRow(_dropZone, 2);

        _receivedFiles = new ListBox
        {
            Background = Brush("#111A31"),
            Foreground = Brush("#F7F8FC"),
            BorderBrush = Brush("#2F3A56"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            FontSize = 14
        };
        _receivedFiles.Items.Add("No physical files received yet.");
        Grid.SetRow(_receivedFiles, 4);

        root.Children.Add(header);
        root.Children.Add(_dropZone);
        root.Children.Add(_receivedFiles);
        Content = root;
    }

    private void HandleDragEnter(object sender, DragEventArgs eventArgs)
    {
        var accepted = eventArgs.Data.GetDataPresent(DataFormats.FileDrop);
        eventArgs.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        eventArgs.Handled = true;
        _dropZone.Background = Brush(accepted ? "#1D2C56" : "#111A31");
    }

    private void HandleDrop(object sender, DragEventArgs eventArgs)
    {
        _dropZone.Background = Brush("#111A31");
        _receivedFiles.Items.Clear();
        if (eventArgs.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            _receivedFiles.Items.Add("Drop arrived without physical file paths.");
            return;
        }

        foreach (var file in files)
        {
            _receivedFiles.Items.Add($"{Path.GetFileName(file)} — {new FileInfo(file).Length:N0} bytes");
        }

        eventArgs.Handled = true;
    }

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
