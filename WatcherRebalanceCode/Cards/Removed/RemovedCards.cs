using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Common;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Removed;


// ============================================================================
// REMOVED WATCHER CARDS
// ============================================================================
//
// Each original Watcher card can now be restored through WatcherRebalance's
// BaseLib configuration.
//
// By default all of these cards remain removed.
//
// IMPORTANT:
//
// These constructor changes are Harmony transpilers, so changing one of the
// Restore Card options requires restarting the game.
//
// Expunger follows Conjure Blade:
// restoring Conjure Blade also restores Expunger.
// ============================================================================


// ============================================================================
// PRESSURE POINTS
// ============================================================================

[HarmonyPatch(
    typeof(PressurePoints),
    MethodType.Constructor)]
public static class RemovePressurePointsPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestorePressurePoints)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(PressurePoints));
    }
}


// ============================================================================
// ORIGINAL RUSHDOWN
// ============================================================================

[HarmonyPatch(
    typeof(Rushdown),
    MethodType.Constructor)]
public static class RemoveOriginalRushdownPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        //if (Config.RestoreRushdown)
        //    return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(Rushdown));
    }
}


// ============================================================================
// CONJURE BLADE
// ============================================================================

[HarmonyPatch(
    typeof(ConjureBlade),
    MethodType.Constructor)]
public static class RemoveConjureBladePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestoreConjureBlade)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(ConjureBlade));
    }
}


// ============================================================================
// JUDGMENT
// ============================================================================

[HarmonyPatch(
    typeof(Judgment),
    MethodType.Constructor)]
public static class RemoveJudgmentPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestoreJudgment)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(Judgment));
    }
}


// ============================================================================
// SCRAWL
// ============================================================================

[HarmonyPatch(
    typeof(ScrawlWatcher),
    MethodType.Constructor)]
public static class RemoveScrawlPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestoreScrawl)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(ScrawlWatcher));
    }
}


// ============================================================================
// WISH
// ============================================================================

[HarmonyPatch(
    typeof(WishWatcher),
    MethodType.Constructor)]
public static class RemoveWishPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestoreWish)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(WishWatcher));
    }
}


// ============================================================================
// EXPUNGER
// ============================================================================
//
// Expunger belongs to Conjure Blade.
//
// It therefore deliberately has NO config option of its own:
//
// RestoreConjureBlade = false
//     -> Expunger stays hidden / unavailable.
//
// RestoreConjureBlade = true
//     -> Expunger returns with Conjure Blade.
// ============================================================================

[HarmonyPatch(
    typeof(Expunger),
    MethodType.Constructor)]
public static class HideExpungerPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        if (Config.RestoreConjureBlade)
            return instructions;

        return RemovedWatcherCardHelper.RemoveCard(
            instructions,
            nameof(Expunger));
    }
}


// ============================================================================
// GENERATION / POOL SAFETY
// ============================================================================

[HarmonyPatch]
public static class RemovedWatcherCardSafetyPatch
{
    // ========================================================================
    // SHOULD THIS CARD CURRENTLY BE REMOVED?
    // ========================================================================

    private static bool IsRemovedWatcherCard(
        CardModel card)
    {
        return card switch
        {
            PressurePoints =>
                !Config.RestorePressurePoints,

            //Rushdown =>
            //    !Config.RestoreRushdown,

            ConjureBlade =>
                !Config.RestoreConjureBlade,

            // Expunger follows Conjure Blade.
            Expunger =>
                !Config.RestoreConjureBlade,

            Judgment =>
                !Config.RestoreJudgment,

            ScrawlWatcher =>
                !Config.RestoreScrawl,

            WishWatcher =>
                !Config.RestoreWish,

            _ =>
                false
        };
    }


    // ========================================================================
    // DISABLE RANDOM COMBAT GENERATION
    // ========================================================================

    [HarmonyPatch(
        typeof(CardModel),
        nameof(CardModel.CanBeGeneratedInCombat),
        MethodType.Getter)]
    [HarmonyPostfix]
    private static void CanBeGeneratedInCombatPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (!IsRemovedWatcherCard(__instance))
            return;

        __result = false;
    }


    // ========================================================================
    // REPORT TOKEN CARD POOL
    // ========================================================================

    [HarmonyPatch(
        typeof(CardModel),
        nameof(CardModel.Pool),
        MethodType.Getter)]
    [HarmonyPrefix]
    private static bool PoolPrefix(
        CardModel __instance,
        ref CardPoolModel __result)
    {
        if (!IsRemovedWatcherCard(__instance))
            return true;

        __result =
            ModelDb.CardPool<TokenCardPool>();

        return false;
    }
}


// ============================================================================
// SHARED CONSTRUCTOR PATCH
// ============================================================================

internal static class RemovedWatcherCardHelper
{
    public static IEnumerable<CodeInstruction> RemoveCard(
        IEnumerable<CodeInstruction> instructions,
        string cardName)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                $"WatcherRebalance: Could not find " +
                $"WatcherCardModel constructor while removing {cardName}.");
        }


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;


            if (i < 5)
            {
                throw new Exception(
                    $"WatcherRebalance: Invalid constructor IL " +
                    $"while removing {cardName}.");
            }


            // ================================================================
            // RARITY
            //
            // Common / Uncommon / Rare -> Token
            // ================================================================

            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Token);


            // ================================================================
            // CARD LIBRARY
            //
            // true -> false
            // ================================================================

            ReplaceInt(
                code,
                i - 1,
                0);


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                $"WatcherRebalance: Failed to remove {cardName}.");
        }


        return code;
    }


    // ========================================================================
    // INTEGER IL HELPER
    // ========================================================================

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
                -1 => new CodeInstruction(
                    OpCodes.Ldc_I4_M1),

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