using GoshehYar.Core.Settings;

namespace GoshehYar.Core.Music;

/// <summary>Decides which track plays next (order, shuffle, repeat). No audio code, so it is easy to test.</summary>
public sealed class PlaybackQueue
{
    private readonly Random _random;
    private List<int> _order = new();
    private int _position = -1;

    public PlaybackQueue(Random? random = null) => _random = random ?? new Random();

    public int Count { get; private set; }

    public bool Shuffle { get; private set; }

    public RepeatMode Repeat { get; set; } = RepeatMode.All;

    /// <summary>Index into the track list of the current track, or -1.</summary>
    public int CurrentIndex => _position >= 0 && _position < _order.Count ? _order[_position] : -1;

    public void Reset(int count, bool shuffle)
    {
        Count = Math.Max(0, count);
        Shuffle = shuffle;
        Rebuild(keepCurrent: -1);
        _position = -1;
    }

    public void SetShuffle(bool shuffle)
    {
        if (Shuffle == shuffle) return;
        var current = CurrentIndex;
        Shuffle = shuffle;
        Rebuild(current);
    }

    /// <summary>Jumps to a given track (user double-click).</summary>
    public void SelectIndex(int index)
    {
        if (index < 0 || index >= Count) return;
        var pos = _order.IndexOf(index);
        if (pos >= 0) _position = pos;
    }

    /// <summary>Next track. <paramref name="auto"/> = the current track ended by itself (Repeat One applies).</summary>
    public int Next(bool auto)
    {
        if (Count == 0) return -1;
        if (auto && Repeat == RepeatMode.One && CurrentIndex >= 0) return CurrentIndex;

        if (_position + 1 < _order.Count)
        {
            _position++;
            return CurrentIndex;
        }

        if (Repeat == RepeatMode.None && auto) return -1;

        // Wrap around (a new shuffled round when shuffling).
        if (Shuffle) Rebuild(keepCurrent: -1);
        _position = 0;
        return CurrentIndex;
    }

    public int Previous()
    {
        if (Count == 0) return -1;
        _position = _position > 0 ? _position - 1 : _order.Count - 1;
        return CurrentIndex;
    }

    private void Rebuild(int keepCurrent)
    {
        _order = Enumerable.Range(0, Count).ToList();
        if (Shuffle)
        {
            for (var i = _order.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        if (keepCurrent >= 0)
        {
            var at = _order.IndexOf(keepCurrent);
            if (at > 0)
            {
                _order.RemoveAt(at);
                _order.Insert(0, keepCurrent);
            }

            _position = at >= 0 ? 0 : -1;
        }
    }
}
