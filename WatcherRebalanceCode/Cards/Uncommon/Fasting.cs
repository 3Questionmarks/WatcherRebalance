using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(Fasting), MethodType.Constructor)]
public static class FastingPatch
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
            // Change base cost from 2 -> 1.
            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(code, i - 5, 1);
                continue;
            }

            if (code[i].operand is not MethodInfo method)
                continue;

            if (method.Name != "WithPower" ||
                !method.IsGenericMethod)
            {
                continue;
            }

            Type[] genericArguments =
                method.GetGenericArguments();

            if (genericArguments.Length != 1)
                continue;

            ParameterInfo[] parameters =
                method.GetParameters();

            // We only want BaseLib's:
            // WithPower<T>(int baseVal, int upgrade)
            //
            // Not Watcher's:
            // WithPower<T>(int baseVal, bool showTooltip)
            if (parameters.Length != 2 ||
                parameters[0].ParameterType != typeof(int) ||
                parameters[1].ParameterType != typeof(int))
            {
                continue;
            }

            Type powerType = genericArguments[0];

            if (powerType == typeof(StrengthPower) ||
                powerType == typeof(DexterityPower))
            {
                // Original: 3, 1
                // New:      3, 1
                ReplaceInt(code, i - 1, 1);
            }
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