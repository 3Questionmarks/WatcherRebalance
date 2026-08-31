using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;

[HarmonyPatch]
public static class DeusExMachinaPatch
{
    // =========================================================
    // ADD SLY
    // =========================================================

    [HarmonyPatch(typeof(DeusExMachina), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        DeusExMachina __instance)
    {
        AddSly(__instance);
    }


    private static void AddSly(
        DeusExMachina card)
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
            withKeyword
                .GetParameters()[1]
                .ParameterType;

        // UpgradeType.None == 0
        object none =
            Enum.ToObject(
                upgradeType,
                0);

        withKeyword.Invoke(
            card,
            [
                CardKeyword.Sly,
                none
            ]);
    }


    // =========================================================
    // ACTUAL PLAY EFFECT
    //
    // Deus does not originally override OnPlay.
    //
    // Patch the base CardModel implementation, but ONLY
    // replace it when the actual card is Deus Ex Machina.
    // =========================================================

    [HarmonyPatch(
        typeof(CardModel),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        CardModel __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        if (__instance is not DeusExMachina card)
            return true;

        __result =
            PlayDeus(
                card);

        return false;
    }


    private static async Task PlayDeus(
        DeusExMachina card)
    {
        await WatcherCmd.GiveCards<Miracle>(
            card.Owner,
            card.DynamicVars.Cards.IntValue,
            PileType.Hand,
            animationTime: 0.1f);
    }


    // =========================================================
    // WHEN DRAWN
    //
    // Instead of directly creating Miracles and exhausting:
    //
    // 1. mark Deus to Exhaust after this play
    // 2. temporarily permit autoplay despite Unplayable
    // 3. actually autoplay the card
    // =========================================================

    [HarmonyPatch(
        typeof(DeusExMachina),
        nameof(DeusExMachina.AfterCardDrawn))]
    [HarmonyPrefix]
    private static bool AfterCardDrawnPrefix(
        DeusExMachina __instance,
        PlayerChoiceContext __0,
        CardModel __1,
        bool __2,
        ref Task __result)
    {
        if (__1 != __instance)
            return true;

        __result =
            PlayWhenDrawn(
                __instance,
                __0);

        return false;
    }


    private static async Task PlayWhenDrawn(
        DeusExMachina card,
        PlayerChoiceContext choiceContext)
    {
        card.ExhaustOnNextPlay = true;

        AllowDeusAutoPlay = true;

        try
        {
            await CardCmd.AutoPlay(
                choiceContext,
                card,
                null);
        }
        finally
        {
            AllowDeusAutoPlay = false;
        }
    }


    // =========================================================
    // UNPLAYABLE BYPASS
    //
    // CardCmd.AutoPlay normally refuses Unplayable cards.
    //
    // We temporarily remove Unplayable ONLY while Deus is
    // starting one of its automatic plays.
    //
    // This supports:
    // - draw-triggered Deus
    // - Sly-discarded Deus
    //
    // It does NOT make Deus manually playable.
    // =========================================================

    private static bool AllowDeusAutoPlay;


    [HarmonyPatch(
        typeof(CardCmd),
        nameof(CardCmd.AutoPlay),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(Creature),
            typeof(AutoPlayType),
            typeof(bool),
            typeof(bool)
        ])]
    [HarmonyPrefix]
    private static void AutoPlayPrefix(
        CardModel __1,
        AutoPlayType __3,
        out bool __state)
    {
        __state = false;

        if (__1 is not DeusExMachina)
            return;

        bool isDrawTriggered =
            AllowDeusAutoPlay;

        bool isSlyTriggered =
            __3 == AutoPlayType.SlyDiscard;

        if (!isDrawTriggered &&
            !isSlyTriggered)
        {
            return;
        }

        if (!__1.Keywords.Contains(
                CardKeyword.Unplayable))
        {
            return;
        }

        __1.RemoveKeyword(
            CardKeyword.Unplayable);

        __state = true;
    }


    [HarmonyPatch(
        typeof(CardCmd),
        nameof(CardCmd.AutoPlay),
        [
            typeof(PlayerChoiceContext),
            typeof(CardModel),
            typeof(Creature),
            typeof(AutoPlayType),
            typeof(bool),
            typeof(bool)
        ])]
    [HarmonyPostfix]
    private static void AutoPlayPostfix(
        CardModel __1,
        bool __state)
    {
        if (!__state)
            return;

        __1.AddKeyword(
            CardKeyword.Unplayable);
    }
}