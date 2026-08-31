using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(CutThroughFate), MethodType.Constructor)]
public static class CutThroughFatePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

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
                "Could not find WatcherCardModel constructor.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor)
                continue;

            if (constructor != watcherCardConstructor)
                continue;

            // Constructor stack immediately before the call:
            //
            // cost
            // CardType
            // CardRarity       <- i - 3
            // TargetType
            // showInLibrary
            // ctor

            ReplaceInt(
                code,
                i - 3,
                (int)CardRarity.Uncommon);

            break;
        }

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
                value);

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}