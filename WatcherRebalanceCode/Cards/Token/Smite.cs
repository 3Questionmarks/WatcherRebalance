using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch(typeof(Smite))]
public static class SmitePatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Smite __instance)
    {
        AddStrengthVar(__instance);
        AddStrengthTooltip(__instance);
        AddDivinityTooltip(__instance);
    }


    // =========================================================
    // ADD 2 -> 3 STRENGTH VARIABLE
    // =========================================================

    private static void AddStrengthVar(
        Smite card)
    {
        MethodInfo? withPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        if (withPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel.WithPower<T>(int, int, bool).");
        }

        withPower
            .MakeGenericMethod(typeof(StrengthPower))
            .Invoke(
                card,
                new object[]
                {
                    2,      // Smite
                    1,      // Smite+
                    false   // Add tooltip separately
                });
    }


    // =========================================================
    // STRENGTH TOOLTIP
    // =========================================================

    private static void AddStrengthTooltip(
        Smite card)
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

        var strengthTip =
            new TooltipSource(
                _ => HoverTipFactory.FromPower<StrengthPower>());

        withTip.Invoke(
            card,
            new object[]
            {
                strengthTip
            });
    }


    // =========================================================
    // DIVINITY TOOLTIP
    // =========================================================

    private static void AddDivinityTooltip(
        Smite card)
    {
        MethodInfo? withStanceTip =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithStanceTip" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 0);

        if (withStanceTip == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel.WithStanceTip.");
        }

        withStanceTip
            .MakeGenericMethod(typeof(DivinityStance))
            .Invoke(
                card,
                null);
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Smite __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedSmite(
                __instance,
                __0,
                __1);

        return false;
    }

    private static async Task PlayRebalancedSmite(
        Smite card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
            return;

        // Preserve Smite's normal attack.
        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_slash")
            .Execute(
                choiceContext);

        // The Strength loss only occurs in Divinity.
        if (!card.Owner.IsInWatcherStance<DivinityStance>())
            return;

        int strengthLoss =
            card.DynamicVars
                .Power<StrengthPower>()
                .IntValue;

        if (strengthLoss <= 0)
            return;

        await PowerCmd.Apply<SmitePower>(
            choiceContext,
            cardPlay.Target,
            strengthLoss,
            card.Owner.Creature,
            card);
    }


    // =========================================================
    // GOLD GLOW WHILE IN DIVINITY
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

        if (__instance is not Smite card)
            return;

        __result =
            card.Owner
                .IsInWatcherStance<DivinityStance>();
    }
}