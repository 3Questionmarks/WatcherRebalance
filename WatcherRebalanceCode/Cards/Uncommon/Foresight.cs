using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class ForesightPatch
{
    [HarmonyPatch(typeof(Foresight), MethodType.Constructor)]
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

        MethodInfo foresightWithPower =
            withPower.MakeGenericMethod(typeof(ForesightPower));

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(foresightWithPower))
                continue;

            // Original:
            // WithPower<ForesightPower>(3, 1, false)
            //
            // New:
            // WithPower<ForesightPower>(3, 0, false)
            ReplaceInt(code, i - 2, 0);
        }

        return code;
    }

    [HarmonyPatch(typeof(Foresight), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(Foresight __instance)
    {
        AddInnateUpgrade(__instance);
    }

    private static void AddInnateUpgrade(Foresight card)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithKeyword" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(CardKeyword));

        if (withKeyword == null)
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithKeyword.");

        Type upgradeType =
            withKeyword.GetParameters()[1].ParameterType;

        object add =
            Enum.Parse(upgradeType, "Add");

        withKeyword.Invoke(
            card,
            [
                CardKeyword.Innate,
                add
            ]);
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