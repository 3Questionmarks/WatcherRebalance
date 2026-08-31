using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Commands;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class ReachHeavenPatch
{
    [HarmonyPatch(typeof(ReachHeaven), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithDamage" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int));

        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage(int, int).");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;

            // Original:
            // WithDamage(10, 5)
            //
            // New:
            // WithDamage(10, 0)
            //
            // Reach Heaven's upgrade is now Scry 2 instead
            // of +5 damage.
            ReplaceInt(
                code,
                i - 1,
                0);

            break;
        }

        return code;
    }

    [HarmonyPatch(typeof(ReachHeaven), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        ReachHeaven __instance)
    {
        AddVars(__instance);
        AddDivinityTooltip(__instance);
        AddConditionalScryTooltip(__instance);
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

        if (card.IsUpgraded)
        {
            await ScryCmd.Execute(
                choiceContext,
                card.Owner,
                card.DynamicVars["Scry"].IntValue);
        }

        if (card.Owner.IsInWatcherStance<DivinityStance>())
        {
            await CardPileCmd.Draw(
                choiceContext,
                card.DynamicVars.Cards.IntValue,
                card.Owner);
        }
    }

    private static void AddVars(
        ReachHeaven card)
    {
        MethodInfo? withVar =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithVar" &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(string) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(int));

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

        if (withVar == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithVar(string, int, int).");
        }

        if (withCards == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithCards(int, int).");
        }

        // Upgrade-only Scry 2.
        withVar.Invoke(
            card,
            ["Scry", 0, 2]);

        // Divinity always draws 2.
        withCards.Invoke(
            card,
            [2, 0]);
    }

    private static void AddDivinityTooltip(
        ReachHeaven card)
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
                "Could not find WatcherCardModel.WithStanceTip.");
        }

        withStanceTip
            .MakeGenericMethod(
                typeof(DivinityStance))
            .Invoke(
                card,
                null);
    }

    private static void AddConditionalScryTooltip(
        ReachHeaven card)
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
                    ? [HoverTipFactory.Static(BaseLibTip.Scry)]
                    : [];

        withTips.Invoke(
            card,
            [tooltipFactory]);
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
