using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibProsperoPkg;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LibProsperoPkg.Desktop.ViewModels;

/// <summary>
/// Abstraction used by the ViewModel to request folder selection without coupling it to a window
/// implementation.
/// </summary>
public interface IFolderDialogService
{
    Task<string?> SelectFolderAsync(string title, string? initialFolder = null);
}

/// <summary>
/// ViewModel for the LibProsperoPKG desktop shell.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private const string DefaultPasscode = "00000000000000000000000000000000";

    /// <summary>Optional UI service used by the browse commands.</summary>
    public IFolderDialogService? FolderDialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string titleId = "PPSA00000";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string contentId = "UP9000-PPSA00000_00-PROSPERO00000000";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string version = "01.00";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string sourceFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private string outputFolder = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> logEntries = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompilePkgCommand))]
    private bool isBuilding;

    [ObservableProperty]
    private string buildStatusMessage = "READY";

    [ObservableProperty]
    private bool isReadyToBuild;

    public MainWindowViewModel()
    {
        AppendLog("LibProsperoPKG desktop shell ready.");
        AppendLog("Select the source and destination folders, then compile the package.");
        RefreshBuildStatus();
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        try
        {
            if (FolderDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for source selection.");
                return;
            }

            string? selectedFolder = await FolderDialogService.SelectFolderAsync(
                "Select the source sce_sys folder",
                SourceFolder);

            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                AppendLog("Source folder selection cancelled.");
                return;
            }

            SourceFolder = NormalizeFolderPath(selectedFolder);
            AppendLog($"Source folder set to: {SourceFolder}");
            RefreshBuildStatus();
        }
        catch (Exception ex)
        {
            AppendLog($"Source folder import failed: {ex.Message}");
            BuildStatusMessage = "SOURCE IMPORT FAILED";
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        try
        {
            if (FolderDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for destination selection.");
                return;
            }

            string? selectedFolder = await FolderDialogService.SelectFolderAsync(
                "Select the destination folder",
                OutputFolder);

            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                AppendLog("Destination folder selection cancelled.");
                return;
            }

            OutputFolder = NormalizeFolderPath(selectedFolder);
            AppendLog($"Destination folder set to: {OutputFolder}");
            RefreshBuildStatus();
        }
        catch (Exception ex)
        {
            AppendLog($"Destination folder import failed: {ex.Message}");
            BuildStatusMessage = "OUTPUT IMPORT FAILED";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompilePackage))]
    private async Task CompilePkgAsync()
    {
        if (!CanCompilePackage())
        {
            AppendLog("Build blocked: required fields are missing or invalid.");
            return;
        }

        IsBuilding = true;
        BuildStatusMessage = "BUILDING...";
        LogEntries.Clear();
        AppendLog("Build started.");
        AppendLog($"Title: {Title}");
        AppendLog($"Title ID: {TitleId}");
        AppendLog($"Content ID: {ContentId}");
        AppendLog($"Version: {Version}");
        AppendLog($"Source: {SourceFolder}");
        AppendLog($"Destination: {OutputFolder}");

        try
        {
            ProsperoBuildOptions options = new()
            {
                Mode = ProsperoPackageMode.Application,
                OutputFormat = ProsperoOutputFormat.DebugImage,
                SourceFolder = SourceFolder,
                OutputFolder = OutputFolder,
                ContentId = ContentId,
                Passcode = DefaultPasscode,
                Title = Title,
                TitleId = TitleId,
                Version = Version,
                GenerateParamJsonIfMissing = true,
            };

            ProsperoBuildResult result = await Task.Run(() =>
                ProsperoPackageBuilder.Build(options, AppendLog));

            foreach (string warning in result.Warnings)
            {
                AppendLog($"Warning: {warning}");
            }

            AppendLog($"Build complete: {result.OutputPath}");
            BuildStatusMessage = "BUILD COMPLETE";
        }
        catch (Exception ex)
        {
            AppendLog($"Build failed: {ex.Message}");
            BuildStatusMessage = "BUILD FAILED";
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private bool CanCompilePackage()
    {
        return !IsBuilding && TryGetPreflightError(out _);
    }

    partial void OnTitleChanged(string value) => RefreshBuildStatus();
    partial void OnTitleIdChanged(string value) => RefreshBuildStatus();
    partial void OnContentIdChanged(string value) => RefreshBuildStatus();
    partial void OnVersionChanged(string value) => RefreshBuildStatus();
    partial void OnSourceFolderChanged(string value) => RefreshBuildStatus();
    partial void OnOutputFolderChanged(string value) => RefreshBuildStatus();

    private void RefreshBuildStatus()
    {
        if (IsBuilding)
        {
            BuildStatusMessage = "BUILDING...";
            IsReadyToBuild = false;
            return;
        }

        if (TryGetPreflightError(out string message))
        {
            BuildStatusMessage = message;
            IsReadyToBuild = false;
            return;
        }

        BuildStatusMessage = "READY TO BUILD";
        IsReadyToBuild = true;
    }

    private bool TryGetPreflightError(out string message)
    {
        if (string.IsNullOrWhiteSpace(SourceFolder) || !Directory.Exists(SourceFolder))
        {
            message = "SELECT SOURCE FOLDER";
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            message = "SELECT OUTPUT FOLDER";
            return false;
        }

        if (!ProsperoPackageBuilder.IsValidContentId(ContentId))
        {
            message = "INVALID CONTENT ID";
            return false;
        }

        string ebootPath = Path.Combine(SourceFolder, "eboot.bin");
        if (!File.Exists(ebootPath))
        {
            message = "MISSING ROOT EBOOT.BIN";
            return false;
        }

        FileInfo ebootInfo = new(ebootPath);
        if (ebootInfo.Length == 0)
        {
            message = "ROOT EBOOT.BIN IS EMPTY";
            return false;
        }

        string paramPath = Path.Combine(SourceFolder, "sce_sys", "param.json");
        if (File.Exists(paramPath))
        {
            try
            {
                JsonNode? root = JsonNode.Parse(File.ReadAllText(paramPath));
                string? paramContentId = root?["contentId"]?.GetValue<string>();
                string? paramTitleId = root?["titleId"]?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(paramContentId) &&
                    !string.Equals(paramContentId, ContentId, StringComparison.OrdinalIgnoreCase))
                {
                    message = "PARAM.JSON CONTENT ID DOES NOT MATCH";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(TitleId) &&
                    ProsperoPackageBuilder.IsValidTitleId(TitleId) &&
                    !string.IsNullOrWhiteSpace(paramTitleId) &&
                    !string.Equals(paramTitleId, TitleId, StringComparison.OrdinalIgnoreCase))
                {
                    message = "PARAM.JSON TITLE ID DOES NOT MATCH";
                    return false;
                }
            }
            catch (JsonException)
            {
                message = "PARAM.JSON IS INVALID";
                return false;
            }
            catch (IOException)
            {
                message = "PARAM.JSON IS UNREADABLE";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static string NormalizeFolderPath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

        if (Dispatcher.UIThread.CheckAccess())
        {
            LogEntries.Add(line);
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogEntries.Add(line);
            });
        }
    }
}
