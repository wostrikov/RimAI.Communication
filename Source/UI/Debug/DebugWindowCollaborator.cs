using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.UI;

internal enum DebugViewMode
{
    MainTable,
    GroupedByPawn,
    ActiveRequests
}

internal abstract class DebugWindowCollaborator
{
    internal readonly DebugWindow Owner;

    protected DebugWindowCollaborator(DebugWindow owner)
    {
        Owner = owner;
    }

    protected DebugWindowParts Parts => Owner.Parts;
}

internal sealed class DebugWindowParts
{
    internal readonly DebugWindowFilters Filters;
    internal readonly DebugWindowActiveRequests ActiveRequests;
    internal readonly DebugWindowConsoleTable Console;
    internal readonly DebugWindowDetailsPanel Details;
    internal readonly DebugWindowGroupedTable Grouped;
    internal readonly DebugWindowStats Stats;

    internal DebugWindowParts(DebugWindow owner)
    {
        Filters = new DebugWindowFilters(owner);
        ActiveRequests = new DebugWindowActiveRequests(owner);
        Console = new DebugWindowConsoleTable(owner);
        Details = new DebugWindowDetailsPanel(owner);
        Grouped = new DebugWindowGroupedTable(owner);
        Stats = new DebugWindowStats(owner);
    }
}
