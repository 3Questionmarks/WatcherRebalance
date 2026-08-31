using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class LikeWaterPatch
{
    [HarmonyPatch(typeof(LikeWater), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

        MethodInfo? withPower =
            typeof(WatcherCardModel)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        if (withPower == null)
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower<T>(int, int, bool).");

        MethodInfo likeWaterWithPower =
            withPower.MakeGenericMethod(typeof(LikeWaterPower));

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(likeWaterWithPower))
                continue;

            // 5(7) -> 5(7)
            ReplaceInt(code, i - 3, 5);
            ReplaceInt(code, i - 2, 2);
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
            new CodeInstruction(OpCodes.Ldc_I4, value);

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}