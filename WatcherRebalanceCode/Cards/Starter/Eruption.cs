using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Starter;

[HarmonyPatch]
public static class EruptionPatch
{
    static MethodBase TargetMethod()
    {
        // Avoids needing a compile-time reference to Watcher.dll
        var eruptionType = AccessTools.TypeByName(
            "Watcher.Code.Cards.Basic.Eruption"
        );

        if (eruptionType == null)
            throw new Exception(
                "YourMod: Could not find Watcher.Code.Cards.Basic.Eruption"
            );

        var constructor = AccessTools.Constructor(eruptionType);

        if (constructor == null)
            throw new Exception(
                "YourMod: Could not find Eruption constructor"
            );

        return constructor;
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        var withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[] { typeof(int), typeof(int) }
        );

        var withCostUpgrade = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithCostUpgradeBy",
            new[] { typeof(int) }
        );

        if (withDamage == null)
            throw new Exception(
                "YourMod: Could not find ConstructedCardModel.WithDamage"
            );

        if (withCostUpgrade == null)
            throw new Exception(
                "YourMod: Could not find ConstructedCardModel.WithCostUpgradeBy"
            );

        bool damagePatched = false;
        bool costPatched = false;

        for (int i = 0; i < code.Count; i++)
        {
            // Eruption:
            // WithDamage(9)
            //
            // Because the second argument is optional, IL will effectively
            // contain:
            //
            // ldarg.0
            // ldc.i4.s 9
            // ldc.i4.0       <-- upgrade amount
            // call WithDamage(int, int)
            //
            // Replace that 0 with 5.
            if (code[i].Calls(withDamage))
            {
                code[i - 1] = new CodeInstruction(OpCodes.Ldc_I4_5);
                damagePatched = true;
                continue;
            }

            // Eruption:
            // WithCostUpgradeBy(-1)
            //
            // Replace -1 with 0, so ConstructedUpgrade performs
            // EnergyCost.UpgradeBy(0).
            if (code[i].Calls(withCostUpgrade))
            {
                code[i - 1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                costPatched = true;
            }
        }

        if (!damagePatched)
            throw new Exception(
                "YourMod: Failed to patch Eruption damage upgrade"
            );

        if (!costPatched)
            throw new Exception(
                "YourMod: Failed to patch Eruption cost upgrade"
            );

        return code;
    }
}