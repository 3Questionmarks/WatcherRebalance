using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch]
public static class ConsecrateVfxPatch
{
    static MethodBase TargetMethod()
    {
        MethodInfo onPlay =
            AccessTools.Method(
                typeof(Consecrate),
                "OnPlay")
            ?? throw new Exception(
                "WatcherRebalance: Could not find Consecrate.OnPlay");

        Type stateMachineType =
            onPlay.GetCustomAttribute<AsyncStateMachineAttribute>()
                ?.StateMachineType
            ?? throw new Exception(
                "WatcherRebalance: Could not find Consecrate async state machine");

        return AccessTools.Method(
                   stateMachineType,
                   "MoveNext")
               ?? throw new Exception(
                   "WatcherRebalance: Could not find Consecrate.MoveNext");
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr &&
                instruction.operand is string value &&
                value == "vfx/vfx_attack_slash")
            {
                instruction.operand =
                    "vfx/vfx_stabby_line_particles";
            }

            yield return instruction;
        }
    }
}