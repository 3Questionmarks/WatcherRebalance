using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// REMOVED RARE CARDS
// =============================================================
//
// These cards are not deleted from the Watcher mod.
//
// Instead:
//
//     rarity:
//         Rare -> Token
//
//     shouldShowInCardLibrary:
//         true -> false
//
// This removes them from normal Watcher card acquisition while
// leaving their actual model types intact for compatibility.
// =============================================================


// =============================================================
// CONJURE BLADE
// =============================================================

[HarmonyPatch(
    typeof(ConjureBlade),
    MethodType.Constructor)]
public static class RemoveConjureBladePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return RemovedRareCardHelper.RemoveCard(
            instructions,
            nameof(ConjureBlade));
    }
}


// =============================================================
// JUDGMENT
// =============================================================

[HarmonyPatch(
    typeof(Judgment),
    MethodType.Constructor)]
public static class RemoveJudgmentPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return RemovedRareCardHelper.RemoveCard(
            instructions,
            nameof(Judgment));
    }
}


// =============================================================
// SCRAWL
// =============================================================

[HarmonyPatch(
    typeof(ScrawlWatcher),
    MethodType.Constructor)]
public static class RemoveScrawlPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return RemovedRareCardHelper.RemoveCard(
            instructions,
            nameof(ScrawlWatcher));
    }
}


// =============================================================
// WISH
// =============================================================

[HarmonyPatch(
    typeof(WishWatcher),
    MethodType.Constructor)]
public static class RemoveWishPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return RemovedRareCardHelper.RemoveCard(
            instructions,
            nameof(WishWatcher));
    }
}


// =============================================================
// SHARED REMOVAL HELPER
// =============================================================

internal static class RemovedRareCardHelper
{
    public static IEnumerable<CodeInstruction> RemoveCard(
        IEnumerable<CodeInstruction> instructions,
        string cardName)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        ConstructorInfo? watcherCardConstructor =
            typeof(WatcherCardModel)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(constructor =>
                    constructor.GetParameters().Length == 5);


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                $"WatcherRebalance: Could not find WatcherCardModel constructor while removing {cardName}.");
        }


        bool patched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;


            // =================================================
            // RARITY: Rare -> Token
            // =================================================
            //
            // Constructor arguments:
            //
            //     cost
            //     type
            //     rarity                  <- i - 3
            //     target
            //     shouldShowInCardLibrary <- i - 1
            // =================================================

            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Token);


            // =================================================
            // HIDE FROM CARD LIBRARY
            // =================================================

            ReplaceInt(
                code,
                i - 1,
                0);


            patched = true;
            break;
        }


        if (!patched)
        {
            throw new Exception(
                $"WatcherRebalance: Failed to remove {cardName}.");
        }


        return code;
    }


    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];


        CodeInstruction replacement =
            value switch
            {
                0 => new CodeInstruction(
                    OpCodes.Ldc_I4_0),

                1 => new CodeInstruction(
                    OpCodes.Ldc_I4_1),

                2 => new CodeInstruction(
                    OpCodes.Ldc_I4_2),

                3 => new CodeInstruction(
                    OpCodes.Ldc_I4_3),

                4 => new CodeInstruction(
                    OpCodes.Ldc_I4_4),

                5 => new CodeInstruction(
                    OpCodes.Ldc_I4_5),

                6 => new CodeInstruction(
                    OpCodes.Ldc_I4_6),

                7 => new CodeInstruction(
                    OpCodes.Ldc_I4_7),

                8 => new CodeInstruction(
                    OpCodes.Ldc_I4_8),

                _ => new CodeInstruction(
                    OpCodes.Ldc_I4,
                    value)
            };


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        code[index] =
            replacement;
    }
}