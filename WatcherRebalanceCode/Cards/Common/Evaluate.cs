using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(Evaluate))]
public static class EvaluatePatch
{
    /*
     * EVALUATE REBALANCE
     *
     * Base:
     * Gain 8 Block.
     * Shuffle an Insight into your Draw Pile.
     *
     * Upgrade:
     * Gain 10 Block.
     * Add an Insight to the top of your Draw Pile.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Original:
    // WithBlock(6, 4);
    //
    // New:
    // WithBlock(8, 2);
    //

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withBlock = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithBlock",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? replacement = AccessTools.Method(
            typeof(EvaluatePatch),
            nameof(ReplaceBlock)
        );

        if (withBlock == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithBlock."
            );
        }

        if (replacement == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find EvaluatePatch.ReplaceBlock."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withBlock))
                continue;

            var newInstruction = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replacement
            );

            newInstruction.labels.AddRange(code[i].labels);
            newInstruction.blocks.AddRange(code[i].blocks);

            code[i] = newInstruction;

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Evaluate's Block."
            );
        }

        return code;
    }


    private static ConstructedCardModel ReplaceBlock(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withBlock = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithBlock",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        if (withBlock == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithBlock."
            );
        }

        object? result = withBlock.Invoke(
            card,
            new object[]
            {
                8, // Base Block
                2  // Upgrade: 8 -> 10
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithBlock returned an unexpected result."
            );
        }

        return constructedCard;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        Evaluate __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedEvaluate(
            __instance,
            choiceContext,
            cardPlay
        );

        return false;
    }


    private static async Task PlayRebalancedEvaluate(
        Evaluate card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // Gain 8 / 10 Block.
        await CommonActions.CardBlock(
            card,
            cardPlay
        );


        // Base Evaluate:
        // Shuffle the Insight randomly into the Draw Pile.
        //
        // Evaluate+:
        // Put the Insight directly on top of the Draw Pile.

        CardPilePosition position =
            card.IsUpgraded
                ? CardPilePosition.Top
                : CardPilePosition.Random;


        await WatcherCmd.GiveCard<Insight>(
            card.Owner,
            PileType.Draw,
            position
        );
    }
}