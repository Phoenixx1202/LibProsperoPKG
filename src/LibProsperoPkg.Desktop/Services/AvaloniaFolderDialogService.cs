using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibProsperoPkg.Desktop.Services;

public sealed class AvaloniaFolderDialogService : ViewModels.IPathDialogService
{
    private readonly IStorageProvider storageProvider;

    public AvaloniaFolderDialogService(IStorageProvider storageProvider)
    {
        this.storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    }

    public async Task<string?> SelectFolderAsync(string title, string? initialFolder = null)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(initialFolder))
        {
            try
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(initialFolder);
            }
            catch
            {
                // If the path cannot be resolved on the current platform, fall back to the default picker location.
            }
        }

        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(options);
        if (folders.Count == 0)
        {
            return null;
        }

        return folders[0].TryGetLocalPath();
    }

    public async Task<string?> SelectFileAsync(string title, string? initialFolder = null, string? filePattern = null)
    {
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(filePattern))
        {
            options.FileTypeFilter =
            [
                new FilePickerFileType("Selected file type")
                {
                    Patterns = [filePattern],
                },
                new FilePickerFileType("All files")
                {
                    Patterns = ["*.*"],
                }
            ];
        }

        if (!string.IsNullOrWhiteSpace(initialFolder))
        {
            try
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(initialFolder);
            }
            catch
            {
                // If the path cannot be resolved on the current platform, fall back to the default picker location.
            }
        }

        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath();
    }
}
