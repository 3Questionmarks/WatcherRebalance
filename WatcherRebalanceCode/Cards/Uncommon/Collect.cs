using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class CollectPatch
{
    private const string ReplayThresholdKey =
        "ReplayThreshold";


    // ========================================================================
    // COLLECT REBALANCE
    // ========================================================================
    //
    // Put a normal Miracle into your Hand at the start of your
    // next X(+1) turns.
    //
    // If X is 4 or more, add Replay to those Miracles.
    //
    // Exhaust.
    //
    // Upgrade:
    //
    // - Duration: X -> X+1
    // - Replay threshold remains 4.
    // - Miracle itself is NOT upgraded.
    //
    // NOTE:
    //
    // The actual Miracle generation is handled by:
    //
    //     CollectPowerPatch
    //
    // This file only handles:
    //
    // - Replay threshold.
    // - Replay version of Collect.
    // - Gold glow.
    // - Replacing Collect's original Miracle+ tooltip with Miracle.
    // ========================================================================


    // ========================================================================
    // CONSTRUCTOR - REPLACE ORIGINAL MIRACLE+ TOOLTIP
    // ========================================================================
    //
    // Original Collect does:
    //
    // WithTip(new TooltipSource(_ =>
    // {
    //     var miracle = ...ToMutable();
    //     miracle.UpgradeInternal();
    //     return HoverTipFactory.FromCard(miracle);
    // }));
    //
    // Rather than trying to alter that lambda, intercept the WithTip call
    // itself and substitute our own TooltipSource.
    // ========================================================================

    [HarmonyPatch(
        typeof(Collect),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


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
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithTip(TooltipSource).");
        }


        MethodInfo? replacement =
            AccessTools.Method(
                typeof(CollectPatch),
                nameof(ReplaceOriginalMiracleTooltip));


        if (replacement == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "CollectPatch.ReplaceOriginalMiracleTooltip.");
        }


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withTip))
                continue;


            CodeInstruction original =
                code[i];


            var newInstruction =
                new CodeInstruction(
                    OpCodes.Call,
                    replacement);


            newInstruction.labels.AddRange(
                original.labels);

            newInstruction.blocks.AddRange(
                original.blocks);


            code[i] =
                newInstruction;


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to replace " +
                "Collect's original Miracle+ tooltip.");
        }


        return code;
    }


    // ========================================================================
    // NORMAL MIRACLE TOOLTIP
    // ========================================================================
    //
    // Stack-compatible replacement for:
    //
    //     ConstructedCardModel.WithTip(TooltipSource)
    //
    // We consume the original upgraded-Miracle TooltipSource and replace it
    // with a normal Miracle tooltip.
    // ========================================================================

    private static ConstructedCardModel ReplaceOriginalMiracleTooltip(
        ConstructedCardModel card,
        TooltipSource ignoredOriginalTooltip)
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
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithTip(TooltipSource).");
        }


        var miracleTip =
            new TooltipSource(
                _ =>
                    HoverTipFactory.FromCard<Miracle>(
                        false));


        object? result =
            withTip.Invoke(
                card,
                new object[]
                {
                    miracleTip
                });


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: " +
                "ConstructedCardModel.WithTip returned an unexpected result.");
        }


        return constructedCard;
    }


    // ========================================================================
    // CONSTRUCTOR - REPLAY ADDITIONS
    // ========================================================================

    [HarmonyPatch(
        typeof(Collect),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Collect __instance)
    {
        AddReplayThreshold(
            __instance);

        AddReplayTooltip(
            __instance);
    }


    // ========================================================================
    // REPLAY THRESHOLD
    // ========================================================================
    //
    // Collect:
    //     4
    //
    // Collect+:
    //     4
    //
    // Upgrade value is therefore 0.
    // ========================================================================

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
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithVar(string, int, int).");
        }


        withVar.Invoke(
            card,
            new object[]
            {
                ReplayThresholdKey,

                // Base threshold
                4,

                // Upgrade change:
                // 4 + 0 = 4
                0
            });
    }


    // ========================================================================
    // REPLAY TOOLTIP
    // ========================================================================

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
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithTip(TooltipSource).");
        }


        var replayTip =
            new TooltipSource(
                _ =>
                    HoverTipFactory.Static(
                        StaticHoverTip.ReplayStatic));


        withTip.Invoke(
            card,
            new object[]
            {
                replayTip
            });
    }


    // ========================================================================
    // ON PLAY
    // ========================================================================

    [HarmonyPatch(
        typeof(Collect),
        "OnPlay")]
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
        // ====================================================================
        // ENERGY SPENT
        // ====================================================================

        int energySpent =
            card.ResolveEnergyXValue();


        // ====================================================================
        // DURATION
        // ====================================================================
        //
        // Collect:
        //     X turns
        //
        // Collect+:
        //     X+1 turns
        // ====================================================================

        int turns =
            energySpent;


        if (card.IsUpgraded)
        {
            turns++;
        }


        // ====================================================================
        // REPLAY THRESHOLD
        // ====================================================================
        //
        // Both versions:
        //
        //     X >= 4
        // ====================================================================

        int replayThreshold =
            card.DynamicVars[
                    ReplayThresholdKey]
                .IntValue;


        // ====================================================================
        // REPLAY COLLECT
        // ====================================================================

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


        // ====================================================================
        // NORMAL COLLECT
        // ====================================================================
        //
        // CollectPowerPatch is responsible for making this power generate
        // normal Miracles rather than Miracle+.
        // ====================================================================

        await CommonActions.ApplySelf<CollectPower>(
            ctx,
            card,
            turns);
    }


    // ========================================================================
    // GOLD GLOW
    // ========================================================================
    //
    // Glow when the player currently has enough Energy to meet the
    // Replay threshold.
    //
    // Since the threshold no longer upgrades:
    //
    // Collect:
    //     4+
    //
    // Collect+:
    //     4+
    // ========================================================================

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
            card.DynamicVars[
                    ReplayThresholdKey]
                .IntValue;


        var combatState =
            card.Owner.PlayerCombatState;


        if (combatState == null)
            return;


        __result =
            combatState.Energy >=
            threshold;
    }
}