using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using Watcher.Code.Cards.Basic;
using Watcher.Code.Cards.Ancient;
using WatcherRebalance.WatcherRebalanceCode.Cards.Removed;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;

/// <summary>
/// Select which Watcher starter card Archaic Tooth transforms.
/// BaseLib registers Vigilance -> Revelation; when enabled we replace
/// only this mapping with Eruption -> EruptionRevelation.
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
[HarmonyPriority(Priority.Last)]
internal static class EruptionRevelationSelection
{
    [HarmonyPostfix]
    private static void SelectWatcherTransformation(Dictionary<ModelId, CardModel> __result)
    {
        if (!Config.UseEruptionRevelation)
            return;

        __result.Remove(ModelDb.Card<Vigilance>().Id);
        __result[ModelDb.Card<Eruption>().Id] = ModelDb.Card<EruptionRevelation>();
    }
}

/// <summary>
/// When Eruption's Rebalanced Revelation is selected, hide the upstream
/// Vigilance Revelation from both normal pools and the card library.
/// Disabling this setting restores the upstream card unchanged.
/// </summary>
[HarmonyPatch(typeof(Revelation), MethodType.Constructor)]
internal static class HideUpstreamRevelationSelection
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.UseEruptionRevelation
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(Revelation))
            : instructions;
}
