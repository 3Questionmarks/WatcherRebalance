using System.Reflection;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(CrushJoints))]
public static class CrushJointsPatch
{
    /*
     * CRUSH JOINTS REBALANCE
     *
     * Base:
     * Deal 7 damage.
     * If the last card played this combat was a Skill,
     * apply 2 Vulnerable.
     *
     * Upgrade:
     * Deal 10 damage.
     * Vulnerable remains 2.
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

        if (withDamage == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithDamage."
            );
        }

        MethodInfo? replaceDamage = AccessTools.Method(
            typeof(CrushJointsPatch),
            nameof(ReplaceDamage)
        );

        MethodInfo? replaceVulnerable = AccessTools.Method(
            typeof(CrushJointsPatch),
            nameof(ReplaceVulnerable)
        );

        if (replaceDamage == null || replaceVulnerable == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find Crush Joints replacement methods."
            );
        }

        bool damagePatched = false;
        bool vulnerablePatched = false;

        for (int i = 0; i < code.Count; i++)
        {
            // -------------------------------------------------
            // Replace:
            //
            // WithDamage(8, 2)
            //
            // with:
            //
            // WithDamage(7, 3)
            // -------------------------------------------------

            if (!damagePatched && code[i].Calls(withDamage))
            {
                var replacement = new CodeInstruction(
                    System.Reflection.Emit.OpCodes.Call,
                    replaceDamage
                );

                replacement.labels.AddRange(code[i].labels);
                replacement.blocks.AddRange(code[i].blocks);

                code[i] = replacement;
                damagePatched = true;

                continue;
            }


            // -------------------------------------------------
            // Find:
            //
            // WithPower<VulnerablePower>(1, 1)
            //
            // and replace it with:
            //
            // WithPower<VulnerablePower>(2, 0)
            // -------------------------------------------------

            if (code[i].operand is not MethodInfo method)
                continue;

            if (method.Name != "WithPower")
                continue;

            if (!method.IsGenericMethod)
                continue;

            Type[] genericArguments =
                method.GetGenericArguments();

            if (genericArguments.Length != 1 ||
                genericArguments[0] != typeof(VulnerablePower))
            {
                continue;
            }

            var vulnerableReplacement = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replaceVulnerable
            );

            vulnerableReplacement.labels.AddRange(code[i].labels);
            vulnerableReplacement.blocks.AddRange(code[i].blocks);

            code[i] = vulnerableReplacement;
            vulnerablePatched = true;
        }

        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Crush Joints damage."
            );
        }

        if (!vulnerablePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Crush Joints Vulnerable."
            );
        }

        return code;
    }


    // =========================================================
    // DAMAGE
    // =========================================================

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

        if (withDamage == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithDamage."
            );
        }

        object? result = withDamage.Invoke(
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
                "WatcherRebalance: WithDamage returned an unexpected result."
            );
        }

        return constructedCard;
    }


    // =========================================================
    // VULNERABLE
    // =========================================================

    private static ConstructedCardModel ReplaceVulnerable(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withPower = typeof(ConstructedCardModel)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.NonPublic
            )
            .FirstOrDefault(m =>
                m.Name == "WithPower" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(int) &&
                m.GetParameters()[1].ParameterType == typeof(int)
            );

        if (withPower == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithPower(int, int)."
            );
        }

        object? result = withPower
            .MakeGenericMethod(typeof(VulnerablePower))
            .Invoke(
                card,
                new object[]
                {
                    2, // Base Vulnerable
                    0  // No Vulnerable upgrade
                }
            );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithPower<VulnerablePower> returned an unexpected result."
            );
        }

        return constructedCard;
    }
}