using System.Collections.ObjectModel;

namespace FileTransformer.App.Services;

public sealed class UiLogStore
{
    public ObservableCollection<UiLogEntry> Entries { get; } = [];

    public void Add(UiLogEntry entry)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
        {
            AddCore(entry);
            return;
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() => AddCore(entry));
    }

    private void AddCore(UiLogEntry entry)
    {
        Entries.Insert(0, entry);

        while (Entries.Count > 400)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}
