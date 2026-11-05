namespace GoshehYar.Core.Modules;

/// <summary>
/// A tool that can appear on the floating panel (screenshot, notes, ...). Modules are independent of each other:
/// a failing module must never take down the panel or other modules.
/// Phase 1 ships the registry; concrete modules are added from phase 2.
/// </summary>
public interface IModule
{
    /// <summary>Stable id used in settings, e.g. "notes".</summary>
    string Id { get; }

    /// <summary>Localization key of the module title.</summary>
    string TitleKey { get; }

    /// <summary>Segoe MDL2 Assets glyph shown on the panel.</summary>
    string Glyph { get; }

    int DefaultOrder { get; }
}

public sealed class ModuleRegistry
{
    private readonly List<IModule> _modules = new();

    public IReadOnlyList<IModule> All => _modules;

    public void Register(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_modules.Any(m => string.Equals(m.Id, module.Id, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"A module with id '{module.Id}' is already registered.", nameof(module));
        }

        _modules.Add(module);
    }

    /// <summary>
    /// Enabled modules in the user's saved order. Modules missing from the saved order go last
    /// (by default order, then id). Modules missing from the enabled map count as enabled.
    /// </summary>
    public IReadOnlyList<IModule> GetOrderedEnabled(
        IReadOnlyList<string> savedOrder,
        IReadOnlyDictionary<string, bool> enabled)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < savedOrder.Count; i++)
        {
            positions.TryAdd(savedOrder[i], i);
        }

        return _modules
            .Where(m => !enabled.TryGetValue(m.Id, out var on) || on)
            .OrderBy(m => positions.TryGetValue(m.Id, out var p) ? p : int.MaxValue)
            .ThenBy(m => m.DefaultOrder)
            .ThenBy(m => m.Id, StringComparer.Ordinal)
            .ToList();
    }
}
