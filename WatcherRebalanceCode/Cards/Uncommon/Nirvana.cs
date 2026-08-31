using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class NirvanaPatch
{
    [HarmonyPatch(typeof(Nirvana), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

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
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        if (withPower == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower<T>(int, int, bool).");
        }

        MethodInfo nirvanaWithPower =
            withPower.MakeGenericMethod(
                typeof(NirvanaPower));

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(nirvanaWithPower))
                continue;

            // Original:
            // WithPower<NirvanaPower>(3, 1, false)
            //
            // New:
            // WithPower<NirvanaPower>(3, 2, false)
            ReplaceInt(code, i - 3, 3);
            ReplaceInt(code, i - 2, 2);

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
