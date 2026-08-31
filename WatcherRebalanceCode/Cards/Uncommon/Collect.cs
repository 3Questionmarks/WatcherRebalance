using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class CollectPatch
{
    private const string ReplayThresholdKey = "ReplayThreshold";


    /*
     * COLLECT REBALANCE
     *
     * Put a Miracle+ into your Hand at the start of
     * your next X(+1) turns.
     *
     * If X is 4(3) or more, add Replay to them.
     * Exhaust.
     *
     * Threshold is based on the actual Energy spent:
     *
     * Collect:  4+
     * Collect+: 3+
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(typeof(Collect), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Collect __instance)
    {
        AddReplayThreshold(__instance);
        AddReplayTooltip(__instance);
    }


    private static void AddReplayThreshold(
        Collect card)
    {
        MethodInfo? withVar =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithVar",
                new[]
                {
                    typeof(string),
                    typeof(int),
                    typeof(int)
                });


        if (withVar == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithVar(string, int, int).");
        }


        withVar.Invoke(
            card,
            new object[]
            {
                ReplayThresholdKey,
                4,   // Base threshold
                -1   // Collect+ threshold = 3
            });
    }


    private static void AddReplayTooltip(
        Collect card)
    {
        MethodInfo? withTip =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithTip",
                new[]
                {
                    typeof(TooltipSource)
                });


        if (withTip == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithTip.");
        }


        var replayTip =
            new TooltipSource(
                _ => HoverTipFactory.Static(
                    StaticHoverTip.ReplayStatic));


        withTip.Invoke(
            card,
            new object[]
            {
                replayTip
            });
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(typeof(Collect), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Collect __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedCollect(
                __instance,
                __0);

        return false;
    }


    private static async Task PlayRebalancedCollect(
        Collect card,
        PlayerChoiceContext ctx)
    {
        // Actual Energy spent on X.
        int energySpent =
            card.ResolveEnergyXValue();


        // Original Collect duration.
        int turns =
            energySpent;

        if (card.IsUpgraded)
            turns++;


        /*
         * Collect:
         *     threshold = 4
         *
         * Collect+:
         *     threshold = 3
         */

        int replayThreshold =
            card.DynamicVars[ReplayThresholdKey]
                .IntValue;


        if (energySpent >= replayThreshold)
        {
            await PowerCmd.Apply<CollectReplayPower>(
                ctx,
                card.Owner.Creature,
                turns,
                card.Owner.Creature,
                card);

            return;
        }


        // Below the threshold, preserve the original Collect Power.
        await CommonActions.ApplySelf<CollectPower>(
            ctx,
            card,
            turns);
    }


    // =========================================================
    // GOLD GLOW
    // =========================================================

    [HarmonyPatch(
        typeof(CardModel),
        "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not Collect card)
            return;

        int threshold =
            card.DynamicVars[ReplayThresholdKey]
                .IntValue;

        var combatState =
            card.Owner.PlayerCombatState;

        if (combatState == null)
            return;

        __result =
            combatState.Energy >= threshold;
    }
}