namespace LibProsperoPkg.Desktop.ViewModels;

public sealed record PackageEntryViewModel(
    string Path,
    string SizeText,
    string CompressedSizeText,
    string KindText,
    string FlagsText);
