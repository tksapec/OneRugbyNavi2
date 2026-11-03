using System.Collections.ObjectModel;

namespace OneRugbyNavi2;

public sealed class ScheduleDateGroup : ObservableCollection<MatchItem>
{
    public ScheduleDateGroup(string dateLabel, IEnumerable<MatchItem> matches) : base(matches)
    {
        DateLabel = dateLabel;
    }

    public string DateLabel { get; }
}
