using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Specialized;
using LibProsperoPkg.Desktop.Services;
using LibProsperoPkg.Desktop.ViewModels;

namespace LibProsperoPkg.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel
        {
            PathDialogService = new AvaloniaFolderDialogService(StorageProvider),
        };
        DataContext = viewModel;

        if (viewModel is not null)
        {
            viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
        }

        UpdateWindowStateChrome();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        }

        base.OnClosed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            UpdateWindowStateChrome();
        }
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (LogScrollViewer is null)
                {
                    return;
                }

                double targetY = Math.Max(0, LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height);
                LogScrollViewer.Offset = new Avalonia.Vector(
                    LogScrollViewer.Offset.X,
                    targetY);
            }
            catch
            {
                // Auto-scroll is best-effort; the log should never crash the shell.
            }
        }, DispatcherPriority.Background);
    }

    private void UpdateWindowStateChrome()
    {
        if (ShellBorder is null)
        {
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            ShellBorder.Margin = new Thickness(0);
            ShellBorder.CornerRadius = new CornerRadius(0);
            ShellBorder.BorderThickness = new Thickness(0);
            if (!ShellBorder.Classes.Contains("maximized"))
            {
                ShellBorder.Classes.Add("maximized");
            }
        }
        else
        {
            ShellBorder.Margin = new Thickness(18);
            ShellBorder.CornerRadius = new CornerRadius(30);
            ShellBorder.BorderThickness = new Thickness(1);
            ShellBorder.Classes.Remove("maximized");
        }
    }

}
