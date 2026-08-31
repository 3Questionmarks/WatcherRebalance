using System.Reflection;
using System.Reflection.Emit;
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
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class SimmeringFuryPatch
{
    /*
     * SIMMERING FURY
     *
     * At the start of your next turn,
     * enter Wrath and draw 3 cards.
     *
     * Upgrade:
     * Retain your Hand this turn.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(
        typeof(SimmeringFury),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();

        MethodInfo? watcherWithPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        if (watcherWithPower == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower(int, int, bool).");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not MethodInfo calledMethod)
                continue;

            if (!calledMethod.IsGenericMethod)
                continue;

            if (calledMethod.GetGenericMethodDefinition() != watcherWithPower)
                continue;

            Type[] genericArguments =
                calledMethod.GetGenericArguments();

            if (genericArguments.Length != 1)
                continue;

            if (genericArguments[0] != typeof(DrawCardsNextTurnPower))
                continue;

            // Original:
            // DrawCardsNextTurnPower 2(3)
            //
            // New:
            // DrawCardsNextTurnPower 3 flat.
            ReplaceInt(
                code,
                i - 3,
                3);

            ReplaceInt(
                code,
                i - 2,
                0);

            break;
        }

        return code;
    }


    [HarmonyPatch(
        typeof(SimmeringFury),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        SimmeringFury __instance)
    {
        AddRetainHandVar(__instance);
        AddConditionalRetainTooltip(__instance);
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(
        typeof(SimmeringFury),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        SimmeringFury __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result =
            PlayRebalancedSimmeringFury(
                __instance,
                ctx);

        return false;
    }


    private static async Task PlayRebalancedSimmeringFury(
        SimmeringFury card,
        PlayerChoiceContext ctx)
    {
        // Enter Wrath at the start of next turn.
        await CommonActions
            .ApplySelf<SimmeringRagePower>(
                ctx,
                card);

        // Draw 3 cards at the start of next turn.
        await CommonActions
            .ApplySelf<DrawCardsNextTurnPower>(
                ctx,
                card);


        // -----------------------------------------------------
        // Upgrade:
        //
        // Retain the entire Hand this turn.
        //
        // This is the same RetainHandPower used by vanilla
        // Equilibrium.
        // -----------------------------------------------------

        if (!card.IsUpgraded)
            return;

        int retainAmount =
            card.DynamicVars
                .Power<RetainHandPower>()
                .IntValue;

        if (retainAmount <= 0)
            return;

        await PowerCmd.Apply<RetainHandPower>(
            ctx,
            card.Owner.Creature,
            retainAmount,
            card.Owner.Creature,
            card);
    }


    // =========================================================
    // RETAIN HAND VARIABLE
    // =========================================================

    private static void AddRetainHandVar(
        SimmeringFury card)
    {
        MethodInfo? watcherWithPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        if (watcherWithPower == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower(int, int, bool).");
        }

        watcherWithPower
            .MakeGenericMethod(
                typeof(RetainHandPower))
            .Invoke(
                card,
                new object[]
                {
                    0,      // Base
                    1,      // Upgrade
                    false   // Don't show power tooltip
                });
    }


    // =========================================================
    // UPGRADE-ONLY RETAIN TOOLTIP
    // =========================================================

    private static void AddConditionalRetainTooltip(
        SimmeringFury card)
    {
        MethodInfo? withTips =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithTips" &&
                    m.GetParameters().Length == 1);

        if (withTips == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTips.");
        }

        Func<CardModel, IEnumerable<IHoverTip>> tooltipFactory =
            model =>
                model.IsUpgraded
                    ? new IHoverTip[]
                    {
                        HoverTipFactory.FromKeyword(
                            CardKeyword.Retain)
                    }
                    : Array.Empty<IHoverTip>();

        withTips.Invoke(
            card,
            new object[]
            {
                tooltipFactory
            });
    }


    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];

        var replacement =
            new CodeInstruction(
                OpCodes.Ldc_I4,
                value);

        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);

        code[index] = replacement;
    }
}