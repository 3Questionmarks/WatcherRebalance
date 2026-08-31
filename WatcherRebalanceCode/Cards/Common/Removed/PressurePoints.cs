using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common.Removed;

[HarmonyPatch(typeof(PressurePoints))]
public static class PressurePointsPatch
{
    /*
     * PRESSURE POINTS
     *
     * Removed from the normal Watcher card pool in practice by:
     *
     * - changing rarity from Common -> Token
     * - hiding it from the Card Library / Compendium
     *
     * The card model itself remains registered so anything that
     * happens to reference PressurePoints does not break.
     */

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

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

        if (watcherCardConstructor == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel constructor."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo calledConstructor)
                continue;

            if (calledConstructor != watcherCardConstructor)
                continue;

            /*
             * Constructor stack:
             *
             * this
             * energyCost
             * cardType
             * rarity
             * targetType
             * shouldShowInCardLibrary
             * ctor
             *
             * Immediately before the ctor:
             *
             * i - 4 = cardType
             * i - 3 = rarity
             * i - 2 = targetType
             * i - 1 = shouldShowInCardLibrary
             */

            if (i < 3)
            {
                throw new Exception(
                    "WatcherRebalance: Invalid Pressure Points constructor IL."
                );
            }

            // Common -> Token
            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Token
            );

            // Hide from card library / compendium.
            ReplaceInt(
                code,
                i - 1,
                0
            );

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Pressure Points."
            );
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

        var replacement =
            new CodeInstruction(
                OpCodes.Ldc_I4,
                value
            );

        replacement.labels.AddRange(
            original.labels
        );

        replacement.blocks.AddRange(
            original.blocks
        );

        code[index] = replacement;
    }
}