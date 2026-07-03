using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibProsperoPkg;
using LibProsperoPkg.Desktop.Services;
using Avalonia.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LibProsperoPkg.Desktop.ViewModels;

/// <summary>
/// Abstraction used by the ViewModel to request folder selection without coupling it to a window
/// implementation.
/// </summary>
public interface IPathDialogService
{
    Task<string?> SelectFolderAsync(string title, string? initialFolder = null);
    Task<string?> SelectFileAsync(string title, string? initialFolder = null, string? filePattern = null);
}

/// <summary>
/// ViewModel for the LibProsperoPKG desktop shell.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private const string DefaultPasscode = "00000000000000000000000000000000";

    /// <summary>Optional UI service used by the browse commands.</summary>
    public IPathDialogService? PathDialogService { get; set; }

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExtractPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecompilePackageCommand))]
    private string packageFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExtractPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecompilePackageCommand))]
    private string packageExtractFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecompilePackageCommand))]
    private string packageRecompileOutputFolder = string.Empty;

    [ObservableProperty]
    private string packageStatusMessage = "SELECT A PKG TO INSPECT.";

    [ObservableProperty]
    private string packageTypeLabel = "-";

    [ObservableProperty]
    private string packageContentId = "-";

    [ObservableProperty]
    private string packageTitleId = "-";

    [ObservableProperty]
    private string packageTitle = "-";

    [ObservableProperty]
    private string packageVersion = "-";

    [ObservableProperty]
    private string packageContentVersion = "-";

    [ObservableProperty]
    private string packageMasterVersion = "-";

    [ObservableProperty]
    private string packageDrmType = "-";

    [ObservableProperty]
    private string packageContentType = "-";

    [ObservableProperty]
    private string packageSignedByte = "-";

    [ObservableProperty]
    private string packageBodyOffset = "-";

    [ObservableProperty]
    private string packageBodySize = "-";

    [ObservableProperty]
    private string packageSize = "-";

    [ObservableProperty]
    private string packageEntryCount = "0";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExtractPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecompilePackageCommand))]
    private bool isPackageLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExtractPackageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecompilePackageCommand))]
    private bool isPackageBusy;

    [ObservableProperty]
    private bool hasPackageIcon;

    [ObservableProperty]
    private bool hasPackageBackground;

    [ObservableProperty]
    private Bitmap? packageIcon;

    [ObservableProperty]
    private Bitmap? packageBackground;

    [ObservableProperty]
    private ObservableCollection<PackageEntryViewModel> packageEntries = new();

    [ObservableProperty]
    private ObservableCollection<PackageTreeNodeViewModel> packageTree = new();

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
            if (PathDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for source selection.");
                return;
            }

            string? selectedFolder = await PathDialogService.SelectFolderAsync(
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
            if (PathDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for destination selection.");
                return;
            }

            string? selectedFolder = await PathDialogService.SelectFolderAsync(
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

    [RelayCommand]
    private async Task BrowsePackageAsync()
    {
        try
        {
            if (PathDialogService is null)
            {
                AppendLog("File dialog service is not configured for PKG selection.");
                return;
            }

            string? selectedFile = await PathDialogService.SelectFileAsync(
                "Select a PS5 PKG file",
                string.IsNullOrWhiteSpace(PackageFilePath) ? null : Path.GetDirectoryName(PackageFilePath),
                "*.pkg");

            if (string.IsNullOrWhiteSpace(selectedFile))
            {
                AppendLog("PKG selection cancelled.");
                return;
            }

            PackageFilePath = NormalizePath(selectedFile);
            AppendLog($"PKG file selected: {PackageFilePath}");
            if (string.IsNullOrWhiteSpace(PackageExtractFolder))
            {
                PackageExtractFolder = BuildDefaultExtractionFolder(PackageFilePath);
            }
            if (string.IsNullOrWhiteSpace(PackageRecompileOutputFolder))
            {
                PackageRecompileOutputFolder = BuildDefaultRecompileFolder(PackageFilePath);
            }

            await LoadPackageAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"PKG selection failed: {ex.Message}");
            PackageStatusMessage = "PKG SELECTION FAILED";
        }
    }

    [RelayCommand]
    private async Task BrowsePackageExtractFolderAsync()
    {
        try
        {
            if (PathDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for extraction selection.");
                return;
            }

            string? selectedFolder = await PathDialogService.SelectFolderAsync(
                "Select the extraction folder",
                PackageExtractFolder);

            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                AppendLog("Extraction folder selection cancelled.");
                return;
            }

            PackageExtractFolder = NormalizeFolderPath(selectedFolder);
            AppendLog($"Extraction folder set to: {PackageExtractFolder}");
        }
        catch (Exception ex)
        {
            AppendLog($"Extraction folder selection failed: {ex.Message}");
            PackageStatusMessage = "EXTRACTION FOLDER FAILED";
        }
    }

    [RelayCommand]
    private async Task BrowsePackageRecompileOutputFolderAsync()
    {
        try
        {
            if (PathDialogService is null)
            {
                AppendLog("Folder dialog service is not configured for recompilation output selection.");
                return;
            }

            string? selectedFolder = await PathDialogService.SelectFolderAsync(
                "Select the recompilation output folder",
                PackageRecompileOutputFolder);

            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                AppendLog("Recompilation output folder selection cancelled.");
                return;
            }

            PackageRecompileOutputFolder = NormalizeFolderPath(selectedFolder);
            AppendLog($"Recompilation output folder set to: {PackageRecompileOutputFolder}");
        }
        catch (Exception ex)
        {
            AppendLog($"Recompilation output folder selection failed: {ex.Message}");
            PackageStatusMessage = "RECOMPILE FOLDER FAILED";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadPackage))]
    private async Task LoadPackageAsync()
    {
        if (!CanLoadPackage())
        {
            AppendLog("PKG load blocked: select a valid .pkg file first.");
            return;
        }

        IsPackageBusy = true;
        PackageStatusMessage = "LOADING PKG...";

        try
        {
            PackageInspectionResult result = await Task.Run(() =>
                PackageInspectionService.Inspect(PackageFilePath));

            ApplyPackageResult(result);
            PackageStatusMessage = result.PackageStatusMessage;
            AppendLog($"PKG loaded: {result.PackagePath}");
        }
        catch (Exception ex)
        {
            AppendLog($"PKG load failed: {ex.Message}");
            ResetPackageState();
            PackageStatusMessage = "PKG LOAD FAILED";
        }
        finally
        {
            IsPackageBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExtractPackage))]
    private async Task ExtractPackageAsync()
    {
        if (!CanExtractPackage())
        {
            AppendLog("PKG extraction blocked: load a package and choose an output folder.");
            return;
        }

        IsPackageBusy = true;
        PackageStatusMessage = "EXTRACTING PKG...";

        try
        {
            await Task.Run(() =>
                PackageInspectionService.ExtractFoundFiles(PackageFilePath, PackageExtractFolder, AppendLog));

            AppendLog($"PKG extracted to: {PackageExtractFolder}");
            PackageStatusMessage = "EXTRACTION COMPLETE";
        }
        catch (Exception ex)
        {
            AppendLog($"PKG extraction failed: {ex.Message}");
            PackageStatusMessage = "PKG EXTRACTION FAILED";
        }
        finally
        {
            IsPackageBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecompilePackage))]
    private async Task RecompilePackageAsync()
    {
        if (!CanRecompilePackage())
        {
            AppendLog("PKG recompilation blocked: extract the package first.");
            return;
        }

        IsPackageBusy = true;
        PackageStatusMessage = "RECOMPILING PKG...";

        try
        {
            ProsperoBuildOptions options = new()
            {
                Mode = ProsperoPackageMode.Application,
                OutputFormat = ProsperoOutputFormat.DebugImage,
                SourceFolder = PackageExtractFolder,
                OutputFolder = PackageRecompileOutputFolder,
                ContentId = PackageContentId,
                Passcode = DefaultPasscode,
                Title = PackageTitle,
                TitleId = PackageTitleId,
                Version = PackageVersion,
                GenerateParamJsonIfMissing = true,
            };

            ProsperoBuildResult result = await Task.Run(() =>
                ProsperoPackageBuilder.Build(options, AppendLog));

            foreach (string warning in result.Warnings)
            {
                AppendLog($"Warning: {warning}");
            }

            AppendLog($"Recompile complete: {result.OutputPath}");
            PackageStatusMessage = "RECOMPILE COMPLETE";
        }
        catch (Exception ex)
        {
            AppendLog($"Recompile failed: {ex.Message}");
            PackageStatusMessage = "RECOMPILE FAILED";
        }
        finally
        {
            IsPackageBusy = false;
        }
    }

    private bool CanCompilePackage()
    {
        return !IsBuilding && TryGetPreflightError(out _);
    }

    private bool CanLoadPackage()
    {
        return !IsPackageBusy && !string.IsNullOrWhiteSpace(PackageFilePath) && File.Exists(PackageFilePath);
    }

    private bool CanExtractPackage()
    {
        return !IsPackageBusy && IsPackageLoaded && !string.IsNullOrWhiteSpace(PackageExtractFolder);
    }

    private bool CanRecompilePackage()
    {
        return !IsPackageBusy && IsPackageLoaded && !string.IsNullOrWhiteSpace(PackageExtractFolder) &&
               Directory.Exists(PackageExtractFolder) && !string.IsNullOrWhiteSpace(PackageRecompileOutputFolder);
    }

    partial void OnTitleChanged(string value) => RefreshBuildStatus();
    partial void OnTitleIdChanged(string value) => RefreshBuildStatus();
    partial void OnContentIdChanged(string value) => RefreshBuildStatus();
    partial void OnVersionChanged(string value) => RefreshBuildStatus();
    partial void OnSourceFolderChanged(string value) => RefreshBuildStatus();
    partial void OnOutputFolderChanged(string value) => RefreshBuildStatus();
    partial void OnPackageFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            PackageStatusMessage = "PKG READY TO LOAD";
        }
    }
    partial void OnPackageExtractFolderChanged(string value) { }
    partial void OnPackageRecompileOutputFolderChanged(string value) { }

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

    private static string NormalizePath(string path) => NormalizeFolderPath(path);

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

    private void ApplyPackageResult(PackageInspectionResult result)
    {
        IsPackageLoaded = true;
        PackageContentId = result.PackageContentId;
        PackageTitleId = result.PackageTitleId;
        PackageTitle = result.PackageTitle;
        PackageVersion = result.PackageVersion;
        PackageContentVersion = result.PackageContentVersion;
        PackageMasterVersion = result.PackageMasterVersion;
        PackageDrmType = result.PackageDrmType;
        PackageContentType = result.PackageContentType;
        PackageSignedByte = result.SignedByte;
        PackageBodyOffset = result.PackageBodyOffset;
        PackageBodySize = result.PackageBodySize;
        PackageSize = result.PackageSize;
        PackageEntryCount = result.PackageEntryCount;
        PackageTypeLabel = result.PackageType;
        PackageIcon = result.IconPreview;
        PackageBackground = result.BackgroundPreview;
        HasPackageIcon = PackageIcon is not null;
        HasPackageBackground = PackageBackground is not null;

        PackageEntries.Clear();
        foreach (PackageEntryInfo entry in result.Entries)
        {
            PackageEntries.Add(new PackageEntryViewModel(
                entry.Path,
                FormatBytes(entry.Size),
                FormatBytes(entry.CompressedSize),
                entry.Kind,
                BuildFlagsText(entry)));
        }
        PackageTree = BuildPackageTree(result.Entries);

        if (string.IsNullOrWhiteSpace(PackageExtractFolder))
        {
            PackageExtractFolder = BuildDefaultExtractionFolder(PackageFilePath);
        }

        if (string.IsNullOrWhiteSpace(PackageRecompileOutputFolder))
        {
            PackageRecompileOutputFolder = BuildDefaultRecompileFolder(PackageFilePath);
        }
    }

    private void ResetPackageState()
    {
        IsPackageLoaded = false;
        PackageTypeLabel = "-";
        PackageContentId = "-";
        PackageTitleId = "-";
        PackageTitle = "-";
        PackageVersion = "-";
        PackageContentVersion = "-";
        PackageMasterVersion = "-";
        PackageDrmType = "-";
        PackageContentType = "-";
        PackageSignedByte = "-";
        PackageBodyOffset = "-";
        PackageBodySize = "-";
        PackageSize = "-";
        PackageEntryCount = "0";
        HasPackageIcon = false;
        HasPackageBackground = false;
        PackageIcon = null;
        PackageBackground = null;
        PackageEntries.Clear();
        PackageTree.Clear();
    }

    private static string BuildDefaultExtractionFolder(string packagePath)
    {
        string folder = Path.Combine(
            Path.GetDirectoryName(packagePath) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(packagePath)}_extracted");
        return NormalizeFolderPath(folder);
    }

    private static string BuildDefaultRecompileFolder(string packagePath)
    {
        string folder = Path.Combine(
            Path.GetDirectoryName(packagePath) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(packagePath)}_rebuilt");
        return NormalizeFolderPath(folder);
    }

    private static string FormatBytes(long size)
    {
        if (size < 0)
        {
            return "-";
        }

        if (size < 1024)
        {
            return $"{size} B";
        }

        double value = size;
        string[] units = ["KB", "MB", "GB", "TB"];
        int index = -1;
        do
        {
            value /= 1024.0;
            index++;
        }
        while (value >= 1024.0 && index < units.Length - 1);

        return $"{value:0.##} {units[Math.Max(0, index)]}";
    }

    private static string BuildFlagsText(PackageEntryInfo entry)
    {
        string compression = entry.IsCompressed ? "compressed" : "raw";
        string security = entry.IsEncrypted ? "encrypted" : "open";
        return $"{compression} | {security}";
    }

    private static ObservableCollection<PackageTreeNodeViewModel> BuildPackageTree(IReadOnlyList<PackageEntryInfo> entries)
    {
        var roots = new ObservableCollection<PackageTreeNodeViewModel>();
        var nodeLookup = new Dictionary<string, PackageTreeNodeViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (PackageEntryInfo entry in entries.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            string[] segments = entry.Path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            ObservableCollection<PackageTreeNodeViewModel> children = roots;
            string currentPath = string.Empty;

            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
                bool isLeaf = index == segments.Length - 1;

                if (!nodeLookup.TryGetValue(currentPath, out PackageTreeNodeViewModel? node))
                {
                    node = isLeaf
                        ? new PackageTreeNodeViewModel(
                            name: segment,
                            fullPath: currentPath,
                            kindText: string.IsNullOrWhiteSpace(entry.Kind) ? "File" : entry.Kind,
                            sizeText: FormatBytes(entry.Size),
                            flagsText: BuildFlagsText(entry),
                            isDirectory: false)
                        : new PackageTreeNodeViewModel(
                            name: segment,
                            fullPath: currentPath,
                            kindText: "Folder",
                            sizeText: "-",
                            flagsText: "directory",
                            isDirectory: true);

                    children.Add(node);

                    if (!isLeaf)
                    {
                        nodeLookup[currentPath] = node;
                    }
                }

                children = node.Children;
            }
        }

        return roots;
    }
}
