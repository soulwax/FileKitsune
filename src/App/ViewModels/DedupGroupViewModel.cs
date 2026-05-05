using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileTransformer.App.ViewModels;

public sealed partial class DedupGroupViewModel : ObservableObject
{
    public DedupGroupViewModel(IEnumerable<DedupFileItemViewModel> files)
    {
        Files = new ObservableCollection<DedupFileItemViewModel>(files);
        if (Files.Count > 0 && Files.All(file => !file.IsKeeper))
        {
            Files[0].IsKeeper = true;
        }
    }

    [ObservableProperty]
    private bool isResolved;

    [ObservableProperty]
    private bool isSkipped;

    public ObservableCollection<DedupFileItemViewModel> Files { get; }

    public DedupFileItemViewModel? Keeper => Files.FirstOrDefault(file => file.IsKeeper);

    public int CopyCount => Math.Max(0, Files.Count - 1);

    public long WastedBytes => Files.Where(file => !file.IsKeeper).Sum(file => file.SizeBytes);

    public double WastedMb => WastedBytes / 1024d / 1024d;

    public string CanonicalPath => Keeper?.RelativePath ?? Files.FirstOrDefault()?.RelativePath ?? string.Empty;

    public string DisplayLabel => $"{Path.GetFileName(CanonicalPath)} · {CopyCount} copies · {WastedMb:0.##} MB";

    [RelayCommand]
    public void SetKeeper(DedupFileItemViewModel? selectedFile)
    {
        if (selectedFile is null || !Files.Contains(selectedFile))
        {
            return;
        }

        foreach (var file in Files)
        {
            file.IsKeeper = ReferenceEquals(file, selectedFile);
        }

        IsResolved = false;
        IsSkipped = false;
        OnGroupShapeChanged();
    }

    public void MarkResolved()
    {
        IsResolved = true;
        IsSkipped = false;
        OnPropertyChanged(nameof(DisplayLabel));
    }

    public void MarkSkipped()
    {
        IsResolved = false;
        IsSkipped = true;
        OnPropertyChanged(nameof(DisplayLabel));
    }

    private void OnGroupShapeChanged()
    {
        OnPropertyChanged(nameof(Keeper));
        OnPropertyChanged(nameof(WastedBytes));
        OnPropertyChanged(nameof(WastedMb));
        OnPropertyChanged(nameof(CanonicalPath));
        OnPropertyChanged(nameof(DisplayLabel));
    }
}
