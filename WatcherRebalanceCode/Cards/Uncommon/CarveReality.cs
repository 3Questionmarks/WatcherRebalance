using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class CarveRealityPatch
{
    [HarmonyPatch(typeof(CarveReality), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            [typeof(int), typeof(int)]);

        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;

            // Original: WithDamage(6, 4)
            // New:      WithDamage(6, 3)
            ReplaceInt(code, i - 1, 3);

            break;
        }

        return code;
    }

    [HarmonyPatch(typeof(CarveReality), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        CarveReality __instance)
    {
        AddDivinityTooltip(__instance);
        AddReplayTooltip(__instance);
        WatcherRebalanceTips.AddTokenTip(__instance);
    }

    [HarmonyPatch(typeof(CarveReality), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        CarveReality __instance,
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
    
    // Glow if in Divinity
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not CarveReality card)
            return;

        __result =
            card.Owner
                .IsInWatcherStance<DivinityStance>();
    }

    private static async Task NewOnPlay(
        CarveReality card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Deal damage.
        await CommonActions
            .CardAttack(card, cardPlay)
            .Execute(choiceContext);

        // Add the normal Smite.
        await WatcherCmd.GiveCard<Smite>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Top,
            skipAnimation: true);

        // Replay effect only happens in Divinity.
        if (!card.Owner.IsInWatcherStance<DivinityStance>())
            return;

        // Check that at least one Token exists before opening
        // the selection screen.
        bool hasToken =
            PileType.Hand
                .GetPile(card.Owner)
                .Cards
                .Any(c => c.Rarity == CardRarity.Token);

        if (!hasToken)
            return;

        IEnumerable<CardModel> selectedCards =
            await CardSelectCmd.FromHand(
                choiceContext,
                card.Owner,
                new CardSelectorPrefs(
                    new LocString(
                        "cards",
                        "WATCHER-CARVE_REALITY.selectionScreenPrompt"),
                    1),
                c => c.Rarity == CardRarity.Token,
                card);

        foreach (CardModel selectedCard in selectedCards)
        {
            selectedCard.BaseReplayCount++;
        }
    }

    private static void AddDivinityTooltip(
        CarveReality card)
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
            .MakeGenericMethod(typeof(DivinityStance))
            .Invoke(card, null);
    }

    private static void AddReplayTooltip(
        CarveReality card)
    {
        MethodInfo? withTip = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithTip",
            [typeof(TooltipSource)]);

        if (withTip == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTip.");
        }

        var replayTip = new TooltipSource(
            _ => HoverTipFactory.Static(
                StaticHoverTip.ReplayStatic));

        withTip.Invoke(
            card,
            [replayTip]);
    }

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original = code[index];

        var replacement =
            new CodeInstruction(
                OpCodes.Ldc_I4,
                value);

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}