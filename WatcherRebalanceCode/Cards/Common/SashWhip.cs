using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(SashWhip))]
public static class SashWhipPatch
{
    /*
     * SASH WHIP REBALANCE
     *
     * Deal 7 (10) damage.
     *
     * If the last card played this combat
     * was an Attack, apply 2 Weak.
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
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? replaceDamage = AccessTools.Method(
            typeof(SashWhipPatch),
            nameof(ReplaceDamage)
        );

        MethodInfo? replaceWeak = AccessTools.Method(
            typeof(SashWhipPatch),
            nameof(ReplaceWeak)
        );

        if (withDamage == null ||
            replaceDamage == null ||
            replaceWeak == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find Sash Whip replacement methods."
            );
        }

        bool damagePatched = false;
        bool weakPatched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!damagePatched &&
                code[i].Calls(withDamage))
            {
                var replacement =
                    new CodeInstruction(
                        System.Reflection.Emit.OpCodes.Call,
                        replaceDamage
                    );

                replacement.labels.AddRange(
                    code[i].labels
                );

                replacement.blocks.AddRange(
                    code[i].blocks
                );

                code[i] = replacement;

                damagePatched = true;
                continue;
            }


            if (code[i].operand is not MethodInfo method)
                continue;

            if (method.Name != "WithPower")
                continue;

            if (!method.IsGenericMethod)
                continue;

            Type[] genericArguments =
                method.GetGenericArguments();

            if (genericArguments.Length != 1 ||
                genericArguments[0] != typeof(WeakPower))
            {
                continue;
            }

            var weakReplacement =
                new CodeInstruction(
                    System.Reflection.Emit.OpCodes.Call,
                    replaceWeak
                );

            weakReplacement.labels.AddRange(
                code[i].labels
            );

            weakReplacement.blocks.AddRange(
                code[i].blocks
            );

            code[i] = weakReplacement;

            weakPatched = true;
        }

        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Sash Whip damage."
            );
        }

        if (!weakPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Sash Whip Weak."
            );
        }

        return code;
    }


    private static ConstructedCardModel ReplaceDamage(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        object? result = withDamage?.Invoke(
            card,
            new object[]
            {
                7,
                3
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: Could not replace Sash Whip damage."
            );
        }

        return constructedCard;
    }


    private static ConstructedCardModel ReplaceWeak(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withPower =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic
                )
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(int)
                );

        if (withPower == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find WithPower(int, int)."
            );
        }

        object? result =
            withPower
                .MakeGenericMethod(typeof(WeakPower))
                .Invoke(
                    card,
                    new object[]
                    {
                        2,
                        0
                    }
                );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: Could not replace Sash Whip Weak."
            );
        }

        return constructedCard;
    }
}