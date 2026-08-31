using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class SignatureMovePatch
{
    /*
     * SIGNATURE MOVE
     *
     * Rebalanced:
     *
     * Deal 26(32) damage.
     *
     * Can only be played if the only Attacks in your Hand
     * are copies of Signature Move.
     */


    // =========================================================
    // DAMAGE
    // =========================================================
    //
    // Original constructor:
    //
    //     WithDamage(30, 10);
    //
    // Rebalanced:
    //
    //     WithDamage(26, 6);
    //
    // =========================================================

    [HarmonyPatch(
        typeof(SignatureMove),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        ConstructorTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        bool replacedBaseDamage = false;
        bool replacedUpgradeDamage = false;


        for (int i = 0; i < code.Count; i++)
        {
            // -------------------------------------------------
            // 30 -> 26
            // -------------------------------------------------

            if (!replacedBaseDamage &&
                IsLoadInt(code[i], 30))
            {
                code[i] =
                    LoadIntPreservingMetadata(
                        code[i],
                        26);

                replacedBaseDamage = true;
                continue;
            }


            // -------------------------------------------------
            // 10 -> 6
            // -------------------------------------------------

            if (replacedBaseDamage &&
                !replacedUpgradeDamage &&
                IsLoadInt(code[i], 10))
            {
                code[i] =
                    LoadIntPreservingMetadata(
                        code[i],
                        6);

                replacedUpgradeDamage = true;
                break;
            }
        }


        if (!replacedBaseDamage ||
            !replacedUpgradeDamage)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: Failed to patch " +
                "Signature Move damage.");
        }


        return code;
    }


    // =========================================================
    // PLAYABILITY
    // =========================================================

    [HarmonyPatch(
        typeof(SignatureMove),
        "get_IsPlayable")]
    [HarmonyPrefix]
    private static bool IsPlayablePrefix(
        SignatureMove __instance,
        ref bool __result)
    {
        if (__instance.Owner.PlayerCombatState == null)
        {
            __result = false;
            return false;
        }


        var hand =
            __instance.Owner
                .PlayerCombatState
                .Hand;


        // Signature Move is playable as long as every Attack
        // in the Hand is also a Signature Move.
        //
        // This allows:
        //
        // Signature Move
        // Signature Move
        // Defend
        // Skill
        //
        // but prevents play if something like Strike is present.

        __result =
            hand.Cards.All(card =>
                card.Type != CardType.Attack ||
                card is SignatureMove);


        return false;
    }


    // =========================================================
    // GOLD GLOW
    // =========================================================

    [HarmonyPatch(
        typeof(CardModel),
        "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;


        if (__instance is not SignatureMove card)
            return;


        if (card.Owner.PlayerCombatState == null)
            return;


        __result =
            card.Owner
                .PlayerCombatState
                .Hand
                .Cards
                .All(otherCard =>
                    otherCard.Type != CardType.Attack ||
                    otherCard is SignatureMove);
    }


    // =========================================================
    // IL HELPERS
    // =========================================================

    private static bool IsLoadInt(
        CodeInstruction instruction,
        int value)
    {
        if (instruction.opcode == OpCodes.Ldc_I4)
            return instruction.operand is int intValue &&
                   intValue == value;


        if (instruction.opcode == OpCodes.Ldc_I4_S)
            return instruction.operand is sbyte sbyteValue &&
                   sbyteValue == value;


        return value switch
        {
            -1 => instruction.opcode == OpCodes.Ldc_I4_M1,
            0 => instruction.opcode == OpCodes.Ldc_I4_0,
            1 => instruction.opcode == OpCodes.Ldc_I4_1,
            2 => instruction.opcode == OpCodes.Ldc_I4_2,
            3 => instruction.opcode == OpCodes.Ldc_I4_3,
            4 => instruction.opcode == OpCodes.Ldc_I4_4,
            5 => instruction.opcode == OpCodes.Ldc_I4_5,
            6 => instruction.opcode == OpCodes.Ldc_I4_6,
            7 => instruction.opcode == OpCodes.Ldc_I4_7,
            8 => instruction.opcode == OpCodes.Ldc_I4_8,
            _ => false
        };
    }


    private static CodeInstruction LoadIntPreservingMetadata(
        CodeInstruction original,
        int value)
    {
        CodeInstruction replacement =
            new(
                OpCodes.Ldc_I4,
                value);


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        return replacement;
    }
}