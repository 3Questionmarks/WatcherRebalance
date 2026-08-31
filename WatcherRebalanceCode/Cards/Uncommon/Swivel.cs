using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class SwivelPatch
{
    /*
     * SWIVEL REBALANCE
     *
     * Original:
     * Gain 8(11) Block.
     * The next Attack you play costs 0 Energy.
     *
     * New:
     * Gain 10(13) Block.
     * The next Attack you play costs 0 Energy.
     *
     * FreeAttackPower itself is left completely unchanged.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(
        typeof(Swivel),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? withBlock =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithBlock" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int));


        if (withBlock == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithBlock(int, int).");
        }


        bool patched = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withBlock))
                continue;


            /*
             * Original:
             *
             * WithBlock(8, 3);
             *
             * New:
             *
             * WithBlock(10, 3);
             *
             * Result:
             *
             * Swivel  = 10 Block
             * Swivel+ = 13 Block
             */


            ReplaceInt(
                code,
                i - 2,
                10);


            patched = true;
            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to find Swivel's WithBlock call.");
        }


        return code;
    }


    // =========================================================
    // IL HELPER
    // =========================================================

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