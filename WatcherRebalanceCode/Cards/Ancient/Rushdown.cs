using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Ancient;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;


// ============================================================================
// ANCIENT CARD -> RUSHDOWN
// ============================================================================
//
// Final card:
//
// Rushdown
// Ancient Power
// 1 Energy
//
// Whenever you enter Wrath, draw 2(3) cards.
//
// - Removes Ethereal completely.
// - Removes the original AncientCardPower.
// - Uses the real RushdownPower.
// - Upgrade increases draw from 2 -> 3.
// - Cost remains 1 after upgrading.
//
// NOTE:
//
// The ORIGINAL Uncommon Rushdown is removed by:
//
//     RemovedWatcherCards.cs
//
// Do not add its removal patch back into this file.
// ============================================================================

[HarmonyPatch(typeof(AncientCard))]
public static class RushdownPatch
{
    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // --------------------------------------------------------------------
        // WatcherCardModel constructor
        //
        // AncientCard originally:
        //
        // base(
        //     2,
        //     CardType.Power,
        //     CardRarity.Ancient,
        //     TargetType.None)
        //
        // New:
        //
        // 1 Energy
        // --------------------------------------------------------------------

        ConstructorInfo? watcherConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);

        if (watcherConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }


        // --------------------------------------------------------------------
        // Find original Ethereal keyword call.
        //
        // AncientCard originally registers:
        //
        // WithKeyword(
        //     CardKeyword.Ethereal,
        //     UpgradeType.Remove)
        //
        // We replace it with a no-op helper so Ethereal is never registered.
        // --------------------------------------------------------------------

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


        MethodInfo? removeEthereal =
            AccessTools.Method(
                typeof(RushdownPatch),
                nameof(RemoveEtherealKeyword));

        if (removeEthereal == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find RemoveEtherealKeyword.");
        }


        // --------------------------------------------------------------------
        // Original Ancient power:
        //
        // WithPower<AncientCardPower>(
        //     50,
        //     false)
        //
        // Replace with:
        //
        // WithPower<RushdownPower>(
        //     2,
        //     1,
        //     false)
        //
        // Base:
        //     Draw 2
        //
        // Upgrade:
        //     Draw 3
        // --------------------------------------------------------------------

        MethodInfo? originalWithPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(bool))
                ?.MakeGenericMethod(
                    typeof(AncientCardPower));

        if (originalWithPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WithPower<AncientCardPower>(int, bool).");
        }


        MethodInfo? replacementPower =
            AccessTools.Method(
                typeof(RushdownPatch),
                nameof(ReplaceAncientPower));

        if (replacementPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ReplaceAncientPower.");
        }


        // ====================================================================
        // APPLY PATCHES
        // ====================================================================

        for (int i = 0; i < code.Count; i++)
        {
            // ----------------------------------------------------------------
            // COST
            //
            // 2 -> 1
            // ----------------------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherConstructor)
            {
                ReplaceInt(
                    code,
                    i - 5,
                    1);

                continue;
            }


            // ----------------------------------------------------------------
            // REMOVE ETHEREAL
            // ----------------------------------------------------------------

            if (code[i].Calls(withKeyword))
            {
                CodeInstruction original =
                    code[i];

                var replacement =
                    new CodeInstruction(
                        OpCodes.Call,
                        removeEthereal);

                replacement.labels.AddRange(
                    original.labels);

                replacement.blocks.AddRange(
                    original.blocks);

                code[i] =
                    replacement;

                continue;
            }


            // ----------------------------------------------------------------
            // ANCIENT CARD POWER -> RUSHDOWN POWER
            // ----------------------------------------------------------------

            if (code[i].Calls(originalWithPower))
            {
                CodeInstruction original =
                    code[i];

                var replacement =
                    new CodeInstruction(
                        OpCodes.Call,
                        replacementPower);

                replacement.labels.AddRange(
                    original.labels);

                replacement.blocks.AddRange(
                    original.blocks);

                code[i] =
                    replacement;
            }
        }


        return code;
    }


    // ========================================================================
    // REMOVE ETHEREAL
    // ========================================================================
    //
    // ConstructedCardModel.UpgradeType is protected.
    //
    // At IL level it is represented by an Int32, so this helper accepts the
    // same arguments as WithKeyword but simply returns the card unchanged.
    // ========================================================================

    private static ConstructedCardModel RemoveEtherealKeyword(
        ConstructedCardModel card,
        CardKeyword ignoredKeyword,
        int ignoredUpgradeType)
    {
        return card;
    }


    // ========================================================================
    // REPLACE ANCIENT POWER
    // ========================================================================

    private static ConstructedCardModel ReplaceAncientPower(
        WatcherCardModel card,
        int ignoredAmount,
        bool ignoredShowTooltip)
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
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(bool));

        if (withPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel.WithPower<T>(int, int, bool).");
        }


        object? result =
            withPower
                .MakeGenericMethod(
                    typeof(RushdownPower))
                .Invoke(
                    card,
                    [
                        2,      // Base: draw 2
                        1,      // Upgrade: +1 -> draw 3
                        false
                    ]);


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: " +
                "WithPower<RushdownPower> returned an unexpected result.");
        }


        return constructedCard;
    }


    // ========================================================================
    // ON PLAY
    // ========================================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        AncientCard __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                ctx);

        return false;
    }


    private static async Task NewOnPlay(
        AncientCard card,
        PlayerChoiceContext ctx)
    {
        await CommonActions.ApplySelf<RushdownPower>(
            ctx,
            card);
    }


    // ========================================================================
    // IL HELPER
    // ========================================================================

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

        code[index] =
            replacement;
    }
}