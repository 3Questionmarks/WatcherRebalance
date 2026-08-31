using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class BattleHymnPatch
{
    // ================================================================
    // CONSTRUCTOR
    // ================================================================
    //
    // Original:
    //
    //     Cost 1
    //     Upgrade adds Innate
    //
    // Rebalanced:
    //
    //     Cost 2
    //     Upgrade reduces Cost by 1
    //
    // ================================================================


    // ------------------------------------------------
    // Change base cost:
    //
    //     1 -> 2
    // ------------------------------------------------

    [HarmonyPatch(
        typeof(BattleHymn),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        BattleHymnConstructorTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes =
            instructions.ToList();


        // The first integer passed to the WatcherCardModel
        // constructor is Battle Hymn's energy cost.
        //
        // Original constructor begins:
        //
        //     base(
        //         1,
        //         CardType.Power,
        //         CardRarity.Uncommon,
        //         TargetType.None)
        //
        // Replace that 1 with 2.

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Ldc_I4_1)
            {
                codes[i].opcode =
                    OpCodes.Ldc_I4_2;

                codes[i].operand =
                    null;

                return codes;
            }
        }


        throw new InvalidOperationException(
            "WatcherRebalance: Could not find " +
            "Battle Hymn's base cost in its constructor.");
    }


    // ================================================================
    // UPGRADE
    // ================================================================

    [HarmonyPatch(
        typeof(BattleHymn),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void BattleHymnConstructorPostfix(
        BattleHymn __instance)
    {
        RemoveInnateUpgrade(__instance);
        AddCostUpgrade(__instance);
    }


    // ================================================================
    // REMOVE VANILLA INNATE UPGRADE
    // ================================================================

    private static void RemoveInnateUpgrade(
        BattleHymn card)
    {
        FieldInfo? upgradeKeywordsField =
            AccessTools.Field(
                typeof(ConstructedCardModel),
                "UpgradeKeywords");


        if (upgradeKeywordsField?.GetValue(card)
            is not IList upgradeKeywords)
        {
            throw new MissingFieldException(
                "Could not find " +
                "ConstructedCardModel.UpgradeKeywords.");
        }


        for (int i = upgradeKeywords.Count - 1;
             i >= 0;
             i--)
        {
            object? entry =
                upgradeKeywords[i];


            if (entry == null)
                continue;


            FieldInfo? keywordField =
                entry
                    .GetType()
                    .GetField("Item1");


            if (keywordField?.GetValue(entry)
                    is CardKeyword keyword &&
                keyword == CardKeyword.Innate)
            {
                upgradeKeywords.RemoveAt(i);
            }
        }
    }


    // ================================================================
    // ADD -1 COST UPGRADE
    // ================================================================

    private static void AddCostUpgrade(
        BattleHymn card)
    {
        MethodInfo? withCostUpgradeBy =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithCostUpgradeBy");


        if (withCostUpgradeBy == null)
        {
            throw new MissingMethodException(
                "Could not find " +
                "ConstructedCardModel.WithCostUpgradeBy.");
        }


        withCostUpgradeBy.Invoke(
            card,
            [-1]);
    }
}