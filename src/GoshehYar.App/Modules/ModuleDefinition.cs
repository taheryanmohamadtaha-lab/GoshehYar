using System;
using System.Windows;
using GoshehYar.Core.Modules;

namespace GoshehYar.App.Modules;

/// <summary>A tool on the panel. <see cref="Create"/> builds its window; it returns null if the tool cannot start.</summary>
public sealed class ModuleDefinition : IModule
{
    public ModuleDefinition(string id, string titleKey, string glyph, int order, Func<AppController, Window?> create)
    {
        Id = id;
        TitleKey = titleKey;
        Glyph = glyph;
        DefaultOrder = order;
        Create = create;
    }

    public string Id { get; }
    public string TitleKey { get; }
    public string Glyph { get; }
    public int DefaultOrder { get; }
    public Func<AppController, Window?> Create { get; }
}
