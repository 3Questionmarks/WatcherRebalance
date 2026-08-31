using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(Wallop))]
public static class WallopPatch
{
    /*
     * WALLOP
     *
     * Common
     *
     * Deal 7 (11) damage.
     * Gain Block equal to unblocked damage dealt.
     */

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            [typeof(int), typeof(int)]
        );

        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]
            );

        if (withDamage == null)
            throw new Exception(
                "WatcherRebalance: Could not find WithDamage."
            );

        if (watcherCardConstructor == null)
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel constructor."
            );

        bool damagePatched = false;
        bool rarityPatched = false;

        for (int i = 0; i < code.Count; i++)
        {
            // -------------------------------------------------
            // Damage
            //
            // Original:
            // WithDamage(9, 3)
            //
            // New:
            // WithDamage(7, 4)
            // -------------------------------------------------

            if (!damagePatched &&
                code[i].Calls(withDamage))
            {
                if (i < 2)
                    throw new Exception(
                        "WatcherRebalance: Invalid Wallop damage IL."
                    );

                ReplaceInt(code, i - 2, 7);
                ReplaceInt(code, i - 1, 4);

                damagePatched = true;
            }

            // -------------------------------------------------
            // Rarity
            //
            // WatcherCardModel constructor arguments:
            //
            // cost
            // type
            // rarity       <-- i - 3
            // targetType   <-- i - 2
            // showLibrary  <-- i - 1
            // ctor         <-- i
            // -------------------------------------------------

            if (!rarityPatched &&
                code[i].operand is ConstructorInfo calledCtor &&
                calledCtor == watcherCardConstructor)
            {
                if (i < 3)
                    throw new Exception(
                        "WatcherRebalance: Invalid Wallop rarity IL."
                    );

                ReplaceInt(
                    code,
                    i - 3,
                    (int)CardRarity.Common
                );

                rarityPatched = true;
            }
        }

        if (!damagePatched)
            throw new Exception(
                "WatcherRebalance: Failed to patch Wallop damage."
            );

        if (!rarityPatched)
            throw new Exception(
                "WatcherRebalance: Failed to patch Wallop rarity."
            );

        return code;
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
                value
            );

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}