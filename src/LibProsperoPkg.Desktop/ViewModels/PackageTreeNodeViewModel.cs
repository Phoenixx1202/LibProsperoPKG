using System.Collections.ObjectModel;

namespace LibProsperoPkg.Desktop.ViewModels;

public sealed class PackageTreeNodeViewModel
{
    public PackageTreeNodeViewModel(
        string name,
        string fullPath,
        string kindText,
        string sizeText,
        string flagsText,
        bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        KindText = kindText;
        SizeText = sizeText;
        FlagsText = flagsText;
        IsDirectory = isDirectory;
    }

    public string Name { get; }

    public string FullPath { get; }

    public string KindText { get; }

    public string SizeText { get; }

    public string FlagsText { get; }

    public bool IsDirectory { get; }

    public ObservableCollection<PackageTreeNodeViewModel> Children { get; } = new();
}
