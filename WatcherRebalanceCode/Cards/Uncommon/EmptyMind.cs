using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class EmptyMindPatch
{
    [HarmonyPatch(typeof(EmptyMind), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        EmptyMind __instance)
    {
        AddInsightVar(__instance);
        AddInsightTooltip(__instance);
    }

    [HarmonyPatch(typeof(EmptyMind), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        EmptyMind __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = NewOnPlay(
            __instance,
            ctx,
            cardPlay);

        return false;
    }
    
    // Glow if in stance
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not EmptyMind card)
            return;

        __result = IsInStance(card);
    }

    private static async Task NewOnPlay(
        EmptyMind card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // Preserve original 2(3) draw.
        await CommonActions.Draw(
            card,
            ctx);

        // Check the stance before exiting it.
        if (IsInStance(card))
        {
            int insightCount =
                card.DynamicVars["Insights"].IntValue;

            await WatcherCmd.GiveCards<Insight>(
                card.Owner,
                insightCount,
                PileType.Draw,
                CardPilePosition.Random);
        }

        await StanceCmd.ExitStance(
            ctx,
            card.Owner,
            cardPlay.Card);
    }

    private static bool IsInStance(
        EmptyMind card)
    {
        return
            card.Owner.IsInWatcherStance<CalmStance>() ||
            card.Owner.IsInWatcherStance<WrathStance>() ||
            card.Owner.IsInWatcherStance<DivinityStance>();
    }

    private static void AddInsightVar(
        EmptyMind card)
    {
        MethodInfo? withVar =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithVar",
                [
                    typeof(string),
                    typeof(int),
                    typeof(int)
                ]);

        if (withVar == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithVar(string, int, int).");
        }

        // Shuffle 1(2) Insights.
        withVar.Invoke(
            card,
            ["Insights", 1, 1]);
    }

    private static void AddInsightTooltip(
        EmptyMind card)
    {
        MethodInfo? withTip =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithTip");

        if (withTip == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTip.");
        }

        // ConstructedCardModel.WithTip accepts CardModel types
        // through TooltipSource's implicit conversion.
        withTip.Invoke(
            card,
            [(BaseLib.Utils.TooltipSource)typeof(Insight)]);
    }
}