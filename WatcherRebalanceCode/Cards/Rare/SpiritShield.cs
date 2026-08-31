using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Cards.Rare;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// SPIRIT SHIELD
// =============================================================
//
// Base:
//
//     Gain 3 Block for each card in your Hand.
//     Gain an additional 1 Block for each Token
//     card in your Hand.
//
// Upgrade:
//
//     3 -> 4 Block per card.
//     1 -> 2 additional Block per Token.
//
// This keeps a real CalculatedBlock variable so the card can
// display the TOTAL Block it will gain while in combat.
//
// The original OnPlay is deliberately preserved:
//
//     CommonActions.CardBlock(this, cardPlay)
//
// so the previewed CalculatedBlock and the Block actually gained
// are always the same value.
// =============================================================

[HarmonyPatch(
    typeof(SpiritShield),
    MethodType.Constructor)]
public static class SpiritShieldPatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Original:
    //
    //     WithCalculatedBlock(
    //         0,
    //         3,
    //         Calc,
    //         ValueProp.Move,
    //         0,
    //         1);
    //
    // We replace that entire call with our own setup.
    // =========================================================

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? originalWithCalculatedBlock =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "WithCalculatedBlock")
                        return false;

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    return
                        parameters.Length == 6 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType == typeof(int) &&
                        parameters[2].ParameterType ==
                        typeof(Func<CardModel, Creature?, decimal>) &&
                        parameters[3].ParameterType == typeof(ValueProp) &&
                        parameters[4].ParameterType == typeof(int) &&
                        parameters[5].ParameterType == typeof(int);
                });


        MethodInfo? replacement =
            AccessTools.Method(
                typeof(SpiritShieldPatch),
                nameof(ReplaceCalculatedBlock));


        if (originalWithCalculatedBlock == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find Spirit Shield's WithCalculatedBlock overload.");
        }


        if (replacement == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ReplaceCalculatedBlock.");
        }


        bool patched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(originalWithCalculatedBlock))
                continue;


            // The original instance call leaves this on the stack:
            //
            // card
            // 0
            // 3
            // original Calc delegate
            // ValueProp.Move
            // 0
            // 1
            //
            // Our static replacement accepts exactly those same
            // arguments, consumes them, and builds our variables.
            CodeInstruction original =
                code[i];


            CodeInstruction newInstruction =
                new(
                    OpCodes.Call,
                    replacement);


            newInstruction.labels.AddRange(
                original.labels);

            newInstruction.blocks.AddRange(
                original.blocks);


            code[i] =
                newInstruction;


            patched = true;
            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to replace Spirit Shield's calculated Block.");
        }


        return code;
    }


    // =========================================================
    // REPLACEMENT VARIABLE SETUP
    // =========================================================

    private static ConstructedCardModel ReplaceCalculatedBlock(
        ConstructedCardModel card,
        int ignoredBase,
        int ignoredMultiplier,
        Func<CardModel, Creature?, decimal> ignoredOriginalCalc,
        ValueProp ignoredProps,
        int ignoredUpgrade,
        int ignoredBonusUpgrade)
    {
        // =====================================================
        // CALCULATED TOTAL
        // =====================================================
        //
        // Use the "base + bonus" overload:
        //
        //     WithCalculatedBlock(
        //         int baseVal,
        //         Func<CardModel, Creature?, decimal> bonus,
        //         ValueProp props,
        //         int upgrade,
        //         int bonusUpgrade)
        //
        // The function itself returns the complete Spirit Shield
        // total.
        // =====================================================

        MethodInfo? withCalculatedBlock =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "WithCalculatedBlock")
                        return false;

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    return
                        parameters.Length == 5 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType ==
                        typeof(Func<CardModel, Creature?, decimal>) &&
                        parameters[2].ParameterType == typeof(ValueProp) &&
                        parameters[3].ParameterType == typeof(int) &&
                        parameters[4].ParameterType == typeof(int);
                });


        if (withCalculatedBlock == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCalculatedBlock bonus overload.");
        }


        Func<CardModel, Creature?, decimal> calculateTotal =
            CalculateTotalBlock;


        object? result =
            withCalculatedBlock.Invoke(
                card,
                [
                    0,
                    calculateTotal,
                    ValueProp.Move,
                    0,
                    0
                ]);


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithCalculatedBlock returned an unexpected result.");
        }


        // =====================================================
        // DISPLAY VARIABLES
        // =====================================================
        //
        // These exist purely so localization can say:
        //
        //     3(4) Block per card
        //     +1(2) Block per Token
        //
        // CalculatedBlock above is the actual live total.
        // =====================================================

        MethodInfo? withVar =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "WithVar")
                        return false;

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    return
                        parameters.Length == 3 &&
                        parameters[0].ParameterType == typeof(string) &&
                        parameters[1].ParameterType == typeof(int) &&
                        parameters[2].ParameterType == typeof(int);
                });


        if (withVar == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithVar(string, int, int).");
        }


        // 3 -> 4
        withVar.Invoke(
            constructedCard,
            [
                "BlockPerCard",
                3,
                1
            ]);


        // 1 -> 2
        withVar.Invoke(
            constructedCard,
            [
                "TokenBlock",
                1,
                1
            ]);
        
        // =====================================================
        // TOKEN TOOLTIP
        // =====================================================

        MethodInfo? withTips =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithTips" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(Func<CardModel, IEnumerable<IHoverTip>>));


        if (withTips == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithTips.");
        }


        Func<CardModel, IEnumerable<IHoverTip>> tokenTip =
            _ =>
            [
                WatcherRebalanceTips.Token()
            ];


        withTips.Invoke(
            constructedCard,
            [tokenTip]);


        return constructedCard;
    }


    // =========================================================
    // LIVE BLOCK CALCULATION
    // =========================================================

    private static decimal CalculateTotalBlock(
        CardModel card,
        Creature? creature)
    {
        if (card.Owner.PlayerCombatState == null)
            return 0;


        // Do not count Spirit Shield itself while it is sitting
        // in the Hand.
        //
        // This matches the original card's behavior.
        IEnumerable<CardModel> countedCards =
            card.Owner.PlayerCombatState
                .Hand
                .Cards
                .Where(handCard =>
                    !ReferenceEquals(
                        handCard,
                        card));


        int normalBlockPerCard =
            card.IsUpgraded
                ? 4
                : 3;


        int extraBlockPerToken =
            card.IsUpgraded
                ? 2
                : 1;


        int totalCards = 0;
        int tokenCards = 0;


        foreach (CardModel handCard in countedCards)
        {
            totalCards++;


            if (handCard.Rarity ==
                CardRarity.Token)
            {
                tokenCards++;
            }
        }


        return
            totalCards * normalBlockPerCard +
            tokenCards * extraBlockPerToken;
    }
}