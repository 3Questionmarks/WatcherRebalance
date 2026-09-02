using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class ReachHeavenPatch
{
    // Base: Deal 10 damage. Shuffle Through Violence. Divine: Draw 2.
    // Upgrade: Deal 15 damage. Divine: Draw 3.
    //
    // Reach Heaven already has WithDamage(10, 5), so its original
    // damage upgrade is left intact. The previous upgrade-only Scry
    // effect has been removed completely.

    [HarmonyPatch(typeof(ReachHeaven), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        ReachHeaven __instance)
    {
        AddDrawVar(__instance);
        WatcherRebalanceTips.AddDivineTip(__instance);
    }

    [HarmonyPatch(typeof(ReachHeaven), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        ReachHeaven __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = NewOnPlay(
            __instance,
            choiceContext,
            cardPlay);

        return false;
    }

    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not ReachHeaven card)
            return;

        __result =
            card.Owner
                .IsInWatcherStance<DivinityStance>();
    }

    private static async Task NewOnPlay(
        ReachHeaven card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(card, cardPlay)
            .Execute(choiceContext);

        await WatcherCmd.GiveCard<ThroughViolence>(
            card.Owner,
            PileType.Draw,
            CardPilePosition.Random);

        if (!card.Owner.IsInWatcherStance<DivinityStance>())
            return;

        await CardPileCmd.Draw(
            choiceContext,
            card.DynamicVars.Cards.IntValue,
            card.Owner);
    }

    private static void AddDrawVar(
        ReachHeaven card)
    {
        MethodInfo? withCards =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCards" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int));

        if (withCards == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCards(int, int).");
        }

        // Divine draw: 2 -> 3.
        withCards.Invoke(
            card,
            [2, 1]);
    }
}
