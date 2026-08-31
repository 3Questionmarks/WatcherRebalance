using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Commands;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch(typeof(Insight))]
public static class InsightPatch
{
    /*
     * INSIGHT REBALANCE
     *
     * Base:
     * Retain.
     * If in Divinity, Scry 1.
     * Draw 2 cards.
     * Exhaust.
     *
     * Upgrade:
     * If in Divinity, Scry 2.
     * Draw 3 cards.
     */


    // ---------------------------------------------------------
    // CONSTRUCTOR
    // ---------------------------------------------------------
    //
    // Original Insight:
    //
    // WithCards(2, 1);
    //
    // We intercept that call, preserve it exactly, and then add:
    //
    // WithScry(1, 1);
    //
    // This means both Draw and Scry upgrade from 2 -> 3.
    //

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withCards = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithCards",
            new[] { typeof(int), typeof(int) }
        );

        MethodInfo? replacement = AccessTools.Method(
            typeof(InsightPatch),
            nameof(AddInsightVars)
        );

        if (withCards == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCards."
            );
        }

        if (replacement == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find InsightPatch.AddInsightVars."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withCards))
                continue;

            var newInstruction = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replacement
            );

            // Preserve any Harmony labels/exception blocks attached
            // to the original instruction.
            newInstruction.labels.AddRange(code[i].labels);
            newInstruction.blocks.AddRange(code[i].blocks);

            code[i] = newInstruction;

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to find Insight's WithCards call."
            );
        }

        return code;
    }


    private static ConstructedCardModel AddInsightVars(
        ConstructedCardModel card,
        int baseCards,
        int cardUpgrade)
    {
        /*
         * First reproduce Insight's original:
         *
         * WithCards(2, 1)
         */
        MethodInfo? withCards = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithCards",
            new[] { typeof(int), typeof(int) }
        );

        if (withCards == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithCards."
            );
        }

        object? result = withCards.Invoke(
            card,
            new object[]
            {
                baseCards,
                cardUpgrade
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithCards returned an unexpected result."
            );
        }


        /*
         * Then add Watcher's native Scry variable:
         *
         * Scry 1 -> 2
         */
        MethodInfo? withScry = AccessTools.Method(
            typeof(WatcherCardModel),
            "WithScry",
            new[] { typeof(int), typeof(int) }
        );

        if (withScry == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel.WithScry."
            );
        }

        result = withScry.Invoke(
            card,
            new object[]
            {
                1,
                1
            }
        );

        if (result is not ConstructedCardModel finalCard)
        {
            throw new Exception(
                "WatcherRebalance: WithScry returned an unexpected result."
            );
        }
        
        // Add Divinity hover tooltip.
        MethodInfo? withStanceTip = typeof(WatcherCardModel)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.NonPublic
            )
            .FirstOrDefault(m =>
                m.Name == "WithStanceTip" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 0
            );

        if (withStanceTip == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel.WithStanceTip."
            );
        }

        withStanceTip
            .MakeGenericMethod(typeof(DivinityStance))
            .Invoke(card, null);
        
        return finalCard;
    }


    // ---------------------------------------------------------
    // ON PLAY
    // ---------------------------------------------------------

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        Insight __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedInsight(
            __instance,
            choiceContext
        );

        // Skip Watcher's original Insight.OnPlay.
        return false;
    }
    
    // Glow if in Divinity
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not Insight card)
            return;

        __result =
            card.Owner
                .IsInWatcherStance<DivinityStance>();
    }


    private static async Task PlayRebalancedInsight(
        Insight card,
        PlayerChoiceContext ctx)
    {
        /*
         * While in Divinity:
         *
         * Scry first.
         *
         * This deliberately matches Cut Through Fate's ordering:
         *
         * await ScryCmd.Execute(...);
         * await Draw(...);
         */
        bool isInDivinity =
            card.Owner.IsInWatcherStance<DivinityStance>();

        if (isInDivinity)
        {
            await ScryCmd.Execute(
                ctx,
                card
            );
        }


        /*
         * Then perform Insight's normal draw.
         *
         * Base:    Draw 2.
         * Upgrade: Draw 3.
         */
        await CommonActions.Draw(
            card,
            ctx
        );
    }
}