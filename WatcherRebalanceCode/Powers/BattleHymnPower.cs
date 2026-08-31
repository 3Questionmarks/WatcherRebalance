using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;
using Watcher.Code.Events;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class BattleHymnPowerPatch
{
    // Disable the original:
    // "At the start of each turn, add a Smite to your Hand."
    [HarmonyPatch(
        typeof(BattleHymnPower),
        nameof(BattleHymnPower.BeforeHandDraw))]
    [HarmonyPrefix]
    private static bool DisableOriginalBattleHymnTrigger(
        ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }

    // New effect:
    // "Whenever you change Stances, add a Smite to your Hand."
    [HarmonyPatch(
        typeof(WatcherHook),
        nameof(WatcherHook.OnStanceChange))]
    [HarmonyPostfix]
    private static void BattleHymnOnStanceChangePostfix(
        PlayerChoiceContext ctx,
        Player player,
        ref Task __result)
    {
        __result = TriggerBattleHymnAfterStanceChange(
            __result,
            player);
    }

    private static async Task TriggerBattleHymnAfterStanceChange(
        Task originalTask,
        Player player)
    {
        await originalTask;

        BattleHymnPower? battleHymn =
            player.Creature.GetPower<BattleHymnPower>();

        if (battleHymn == null)
            return;

        int amount = battleHymn.Amount;

        if (amount <= 0)
            return;

        await WatcherCmd.GiveCards<Smite>(
            player,
            amount,
            PileType.Hand,
            CardPilePosition.Top,
            skipAnimation: true);
    }
}