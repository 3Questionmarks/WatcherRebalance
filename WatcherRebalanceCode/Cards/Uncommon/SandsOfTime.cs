using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Uncommon;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class SandsOfTimePatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(typeof(SandsOfTime), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();

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
            // Rebalanced:
            // WithDamage(20, 6) -> 20(26)

            ReplaceInt(
                code,
                i - 1,
                6);

            break;
        }

        return code;
    }

    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(typeof(SandsOfTime), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        SandsOfTime __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayWithCustomFx(
            __instance,
            __0,
            __1);

        return false;
    }

    private static async Task PlayWithCustomFx(
        SandsOfTime card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitFx(
                "vfx/vfx_sandy_impact",
                tmpSfx: "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    // =========================================================
    // HELPER
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

        code[index] =
            replacement;
    }
}