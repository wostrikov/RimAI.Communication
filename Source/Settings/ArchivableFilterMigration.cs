using System.Linq;
using Ustas.RimAI.Communication.Policy;
using Verse;
using Logger = Ustas.RimAI.Communication.Util.Logger;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Moves the event filter's message-def entries from their bare names to the prefixed keys of
/// <see cref="ArchivableFilterKeys"/>, once. Mod settings load before the defs do, so this runs
/// when loading has finished rather than from ExposeData.
/// </summary>
internal static class ArchivableFilterMigration
{
    internal static void RunOnce()
    {
        var settings = Settings.Get();
        if (settings == null || settings.ArchivableFilterKeysMigrated)
            return;

        var messageDefNames = DefDatabase<MessageTypeDef>.AllDefs.Select(def => def.defName).ToList();
        if (messageDefNames.Count == 0)
            return;

        bool moved = ArchivableFilterKeys.MigrateBareMessageKeys(settings.EnabledArchivableTypes, messageDefNames);
        settings.ArchivableFilterKeysMigrated = true;
        settings.Write();
        if (moved)
            Logger.Message("Event filter: message defs now have keys of their own; letters with the same names are back on their defaults.");
    }
}
