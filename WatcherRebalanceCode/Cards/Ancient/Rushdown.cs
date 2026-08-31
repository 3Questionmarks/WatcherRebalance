using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Ancient;
using Watcher.Code.Cards.Uncommon;
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
        // We change the cost:
        //
        // 2 -> 1
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
        // Find the original:
        //
        // WithKeyword(
        //     CardKeyword.Ethereal,
        //     UpgradeType.Remove)
        //
        // We replace this call with our own no-op helper.
        //
        // This removes Ethereal from the card completely without trying to
        // mutate the canonical model after construction.
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
        // Original Ancient Card power:
        //
        // WithPower<AncientCardPower>(
        //     50,
        //     false)
        //
        // We replace this with:
        //
        // WithPower<RushdownPower>(
        //     2,
        //     1,
        //     false)
        //
        // Result:
        //
        // Base:    2
        // Upgrade: 3
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
        // APPLY CONSTRUCTOR PATCHES
        // ====================================================================

        for (int i = 0; i < code.Count; i++)
        {
            // ----------------------------------------------------------------
            // COST
            //
            // Original:
            // 2 Energy
            //
            // New:
            // 1 Energy
            //
            // No cost upgrade is added, so it stays at 1 when upgraded.
            // ----------------------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherConstructor)
            {
                // Stack immediately before WatcherCardModel ctor:
                //
                // this
                // cost
                // type
                // rarity
                // target
                // shouldShowInCardLibrary

                ReplaceInt(
                    code,
                    i - 5,
                    1);

                continue;
            }


            // ----------------------------------------------------------------
            // REMOVE ETHEREAL
            //
            // Instead of allowing:
            //
            // WithKeyword(
            //     CardKeyword.Ethereal,
            //     UpgradeType.Remove)
            //
            // to register Ethereal, replace the method call with a helper
            // that consumes the same stack arguments but returns the card
            // unchanged.
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

                code[i] = replacement;

                continue;
            }


            // ----------------------------------------------------------------
            // AncientCardPower -> RushdownPower
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

                code[i] = replacement;
            }
        }


        return code;
    }


    // ========================================================================
    // REMOVE ETHEREAL
    // ========================================================================
    //
    // ConstructedCardModel.UpgradeType is protected, so our Harmony patch
    // cannot name that enum directly.
    //
    // At IL level the enum is represented as an Int32, so this helper consumes:
    //
    // ConstructedCardModel
    // CardKeyword
    // Int32
    //
    // and simply returns the card without registering the keyword.
    // ========================================================================

    private static ConstructedCardModel RemoveEtherealKeyword(
        ConstructedCardModel card,
        CardKeyword ignoredKeyword,
        int ignoredUpgradeType)
    {
        return card;
    }


    // ========================================================================
    // REPLACE ANCIENT POWER VARIABLE
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
                        false   // Wrath tooltip already exists
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
    //
    // Original:
    //
    // Apply AncientCardPower.
    //
    // New:
    //
    // Apply RushdownPower.
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

        code[index] = replacement;
    }
}


// ============================================================================
// REMOVE ORIGINAL UNCOMMON RUSHDOWN
// ============================================================================
//
// We preserve the original Rushdown model for compatibility, but:
//
// Uncommon -> Token
// visible  -> hidden
//
// This removes it from normal card pools and the compendium.
// ============================================================================

[HarmonyPatch(typeof(Rushdown))]
public static class RemoveOriginalRushdownPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


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


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor ||
                constructor != watcherConstructor)
            {
                continue;
            }


            // ----------------------------------------------------------------
            // Original:
            //
            // CardRarity.Uncommon
            //
            // New:
            //
            // CardRarity.Token
            // ----------------------------------------------------------------

            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Token);


            // ----------------------------------------------------------------
            // Original:
            //
            // shouldShowInCardLibrary = true
            //
            // New:
            //
            // shouldShowInCardLibrary = false
            // ----------------------------------------------------------------

            ReplaceInt(
                code,
                i - 1,
                0);


            break;
        }


        return code;
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

        code[index] = replacement;
    }
}