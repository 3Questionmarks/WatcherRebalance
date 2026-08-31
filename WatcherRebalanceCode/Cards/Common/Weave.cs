using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(Weave))]
public static class WeavePatch
{
    /*
     * WEAVE
     *
     * Common
     *
     * Deal 4 (6) damage.
     * Whenever you Scry, return this from
     * the Discard Pile to your Hand.
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
            // Original: WithDamage(4, 2)
            // New:      WithDamage(4, 2)

            if (!damagePatched &&
                code[i].Calls(withDamage))
            {
                if (i < 2)
                    throw new Exception(
                        "WatcherRebalance: Invalid Weave damage IL."
                    );

                ReplaceInt(code, i - 2, 4);
                ReplaceInt(code, i - 1, 2);

                damagePatched = true;
            }

            if (!rarityPatched &&
                code[i].operand is ConstructorInfo calledCtor &&
                calledCtor == watcherCardConstructor)
            {
                if (i < 3)
                    throw new Exception(
                        "WatcherRebalance: Invalid Weave rarity IL."
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
                "WatcherRebalance: Failed to patch Weave damage."
            );

        if (!rarityPatched)
            throw new Exception(
                "WatcherRebalance: Failed to patch Weave rarity."
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