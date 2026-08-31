using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class SandsOfTimePatch
{
    [HarmonyPatch(typeof(SandsOfTime), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithDamage" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int));

        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage(int, int).");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;

            // Original:
            // WithDamage(20, 6) -> 20(26)
            //
            // New:
            // WithDamage(20, 6) -> 20(26)
            ReplaceInt(
                code,
                i - 1,
                10);

            break;
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
                value);

        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);

        code[index] = replacement;
    }
}
