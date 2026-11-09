using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Watcher.Code.Cards.Multiplayer;
using WatcherRebalance.WatcherRebalanceCode.Cards.Removed;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

// These are startup-time card-pool switches. Changes require restarting the game.
// Apply the same established constructor transformation used for removed Watcher cards,
// so disabled cards cannot enter rewards and do not appear in the card library.
[HarmonyPatch(typeof(GiftOfMiracles), MethodType.Constructor)]
internal static class GiftOfMiraclesSelectionPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.UseOmnipresence
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(GiftOfMiracles))
            : instructions;
}

[HarmonyPatch(typeof(SharedForesight), MethodType.Constructor)]
internal static class SharedForesightSelectionPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.EnableManipulateReality
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(SharedForesight))
            : instructions;
}

[HarmonyPatch(typeof(HoldFast), MethodType.Constructor)]
internal static class HoldFastSelectionPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.UseLegacyMultiplayerStanceCards
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(HoldFast))
            : instructions;
}

[HarmonyPatch(typeof(StanceBlessing), MethodType.Constructor)]
internal static class StanceBlessingSelectionPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => Config.UseLegacyMultiplayerStanceCards
            ? RemovedWatcherCardHelper.RemoveCard(instructions, nameof(StanceBlessing))
            : instructions;
}
