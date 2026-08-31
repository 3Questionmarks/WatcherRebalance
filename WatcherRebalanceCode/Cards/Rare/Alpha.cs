using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;

[HarmonyPatch(typeof(Alpha))]
public static class AlphaPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Alpha __instance)
    {
        AddEthereal(__instance);
        AddDraw(__instance);
    }

    private static void AddEthereal(
        Alpha card)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithKeyword" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(CardKeyword));

        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithKeyword.");
        }

        Type upgradeType =
            withKeyword.GetParameters()[1].ParameterType;

        // UpgradeType.None = 0.
        object none =
            Enum.ToObject(
                upgradeType,
                0);

        withKeyword.Invoke(
            card,
            [
                CardKeyword.Ethereal,
                none
            ]);
    }

    private static void AddDraw(
        Alpha card)
    {
        MethodInfo? withCards =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCards" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                    typeof(int));

        if (withCards == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCards.");
        }

        withCards.Invoke(
            card,
            [
                1,
                0
            ]);
    }

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Alpha __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedAlpha(
                __instance,
                __0);

        return false;
    }

    private static async Task PlayRebalancedAlpha(
        Alpha card,
        PlayerChoiceContext choiceContext)
    {
        await WatcherCmd.GiveCard<Beta>(
            card.Owner,
            PileType.Draw,
            CardPilePosition.Random);

        await CommonActions.Draw(
            card,
            choiceContext);
    }
}