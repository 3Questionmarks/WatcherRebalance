using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class CollectPowerPatch
{
    // ========================================================================
    // TOOLTIP
    // ========================================================================
    //
    // CollectPower should show:
    //
    // - Collect
    // - Normal Miracle
    //
    // NOT Miracle+.
    // ========================================================================

    [HarmonyPatch(
        typeof(PowerModel),
        "get_ExtraHoverTips")]
    [HarmonyPostfix]
    private static void ExtraHoverTipsPostfix(
        PowerModel __instance,
        ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not CollectPower)
            return;


        __result =
            __result.Concat(
                new IHoverTip[]
                {
                    HoverTipFactory.FromCard<Collect>(),
                    HoverTipFactory.FromCard<Miracle>(false)
                });
    }


    // ========================================================================
    // MIRACLE GENERATION
    // ========================================================================
    //
    // Original CollectPower:
    //
    //     upgraded: true
    //
    // New CollectPower:
    //
    //     upgraded: false
    //
    // Everything else remains identical.
    // ========================================================================

    [HarmonyPatch(
        typeof(CollectPower),
        nameof(CollectPower.BeforeHandDraw))]
    [HarmonyPrefix]
    private static bool BeforeHandDrawPrefix(
        CollectPower __instance,
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState,
        ref Task __result)
    {
        __result =
            RebalancedBeforeHandDraw(
                __instance,
                player);

        return false;
    }


    private static async Task RebalancedBeforeHandDraw(
        CollectPower power,
        Player player)
    {
        // Only trigger for the player who owns this CollectPower.
        if (player != power.Owner.Player)
            return;


        // Generate a NORMAL Miracle.
        //
        // Vanilla CollectPower passes:
        //
        //     upgraded: true
        //
        // which produces Miracle+.
        //
        // We deliberately use false.
        await WatcherCmd.GiveCard<Miracle>(
            power.Owner.Player!,
            PileType.Hand,
            CardPilePosition.Top,
            upgraded: false,
            skipAnimation: true);


        // Preserve vanilla CollectPower duration behavior.
        await PowerCmd.Decrement(
            power);
    }
}