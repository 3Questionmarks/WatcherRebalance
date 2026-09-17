using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Events;
using Watcher.Code.Powers;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class LikeWaterPowerPatch
{
    // Disable the original:
    // "At end of turn, if in Calm, gain Block."
    [HarmonyPatch(
        typeof(LikeWaterPower),
        nameof(LikeWaterPower.BeforeSideTurnEndEarly))]
    [HarmonyPrefix]
    private static bool DisableOriginalEffect(
        ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    // New:
    // Whenever you ENTER or EXIT Calm, gain Block.
    [HarmonyPatch(
        typeof(WatcherHook),
        nameof(WatcherHook.OnStanceChange))]
    [HarmonyPostfix]
    private static void OnStanceChangePostfix(
        PlayerChoiceContext ctx,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance,
        ref Task __result)
    {
        __result = HandleStanceChange(
            __result,
            player,
            oldStance,
            newStance);
    }

    private static async Task HandleStanceChange(
        Task original,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        await original;

        bool wasCalm = oldStance is CalmStance;
        bool isCalm = newStance is CalmStance;

        // Trigger only when crossing the Calm boundary:
        //
        // non-Calm -> Calm = entering Calm
        // Calm -> non-Calm = exiting Calm
        //
        // Calm -> Calm and non-Calm -> non-Calm do nothing.
        if (wasCalm == isCalm)
            return;

        LikeWaterPower? power =
            player.Creature.GetPower<LikeWaterPower>();

        if (power == null)
            return;

        await CreatureCmd.GainBlock(
            player.Creature,
            power.Amount,
            ValueProp.Unpowered,
            null);
    }
}