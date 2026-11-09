using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Watcher.Code.Cards.Ancient;
using WatcherRebalance.WatcherRebalanceCode.Cards.Removed;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;

// Mutually exclusive Ancient card pools. Config changes require a game restart.
[HarmonyPatch(typeof(Clairvoyance), MethodType.Constructor)]
internal static class AncientRushdownSelection
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.UseAncientRushdown
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(Clairvoyance))
            : instructions;
}
