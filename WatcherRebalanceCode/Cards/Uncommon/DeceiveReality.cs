using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class DeceiveRealityPatch
{
    [HarmonyPatch(typeof(DeceiveReality), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        MethodInfo? withBlock = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithBlock",
            [typeof(int), typeof(int)]);

        if (withBlock == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithBlock.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withBlock))
                continue;

            // Original: WithBlock(4, 3)
            // New:      WithBlock(4, 0)
            ReplaceInt(code, i - 1, 0);
            break;
        }

        return code;
    }


    [HarmonyPatch(typeof(DeceiveReality), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        DeceiveReality __instance)
    {
        WatcherRebalanceTips.AddTokenTip(__instance);
    }

    [HarmonyPatch(typeof(DeceiveReality), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        DeceiveReality __instance,
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

    private static async Task NewOnPlay(
        DeceiveReality card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions.CardBlock(card, cardPlay);

        await WatcherCmd.GiveCard<Safety>(
            card.Owner,
            PileType.Hand,
            CardPilePosition.Top,
            skipAnimation: true);

        if (!card.IsUpgraded)
            return;

        bool hasUpgradableToken =
            PileType.Hand
                .GetPile(card.Owner)
                .Cards
                .Any(c =>
                    c.Rarity == CardRarity.Token &&
                    c.IsUpgradable);

        if (!hasUpgradableToken)
            return;

        IEnumerable<CardModel> selectedCards =
            await CardSelectCmd.FromHand(
                choiceContext,
                card.Owner,
                new CardSelectorPrefs(
                    new LocString(
                        "cards",
                        "WATCHER-DECEIVE_REALITY.selectionScreenPrompt"),
                    1),
                c =>
                    c.Rarity == CardRarity.Token &&
                    c.IsUpgradable,
                card);

        foreach (CardModel selectedCard in selectedCards)
        {
            CardCmd.Upgrade(selectedCard);
        }
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