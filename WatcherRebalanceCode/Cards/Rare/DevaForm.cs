using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Rare;
using Watcher.Code.Core;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// DEVA FORM - REMOVE ORIGINAL ENERGY VARIABLE / TOOLTIP
// =============================================================
//
// Original Deva Form:
//
//     WithEnergy(1);
//
// Actual signature:
//
//     WithEnergy(int baseVal, int upgrade = 0)
//
// The compiler therefore emits both arguments:
//
//     WithEnergy(1, 0)
//
// Rather than deleting IL instructions, replace the call with a
// helper having the exact same stack signature.
//
// This is the same style of method-call replacement we've used
// successfully in other card patches.
// =============================================================

[HarmonyPatch(
    typeof(DevaForm),
    MethodType.Constructor)]
public static class DevaFormRemoveEnergyTooltipPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? withEnergy =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithEnergy" &&
                    method.GetParameters().Length == 2 &&
                    method.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    method.GetParameters()[1].ParameterType ==
                        typeof(int));


        MethodInfo? replacement =
            AccessTools.Method(
                typeof(DevaFormRemoveEnergyTooltipPatch),
                nameof(RemoveEnergy));


        if (withEnergy == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithEnergy(int, int).");
        }


        if (replacement == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "DevaFormRemoveEnergyTooltipPatch.RemoveEnergy.");
        }


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withEnergy))
                continue;


            CodeInstruction original =
                code[i];


            CodeInstruction newInstruction =
                new(
                    OpCodes.Call,
                    replacement);


            newInstruction.labels.AddRange(
                original.labels);

            newInstruction.blocks.AddRange(
                original.blocks);


            code[i] =
                newInstruction;


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to remove " +
                "Deva Form's Energy variable.");
        }


        return code;
    }


    private static ConstructedCardModel RemoveEnergy(
        ConstructedCardModel card,
        int ignoredBase,
        int ignoredUpgrade)
    {
        // Consume the original WithEnergy arguments but do
        // not create an EnergyVar or Energy tooltip.
        return card;
    }
}


// =============================================================
// DEVA FORM - CARD TOOLTIPS
// =============================================================
//
// Add previews for:
//
// - Calm
// - Wrath
// - Divinity
// - Dexterity
// - Strength
// - Intangible
//
// Energy is deliberately omitted.
// =============================================================

[HarmonyPatch(
    typeof(DevaForm),
    MethodType.Constructor)]
public static class DevaFormTooltipPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        DevaForm __instance)
    {
        MethodInfo? withTips =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithTips" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(Func<CardModel, IEnumerable<IHoverTip>>));


        if (withTips == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithTips.");
        }


        Func<CardModel, IEnumerable<IHoverTip>> devaFormTips =
            _ =>
            [
                // Stances
                WatcherHoverTipFactory.FromStance<CalmStance>(),
                WatcherHoverTipFactory.FromStance<WrathStance>(),
                WatcherHoverTipFactory.FromStance<DivinityStance>(),

                // Powers
                HoverTipFactory.FromPower<DexterityPower>(),
                HoverTipFactory.FromPower<StrengthPower>(),
                HoverTipFactory.FromPower<IntangiblePower>()
            ];


        withTips.Invoke(
            __instance,
            [devaFormTips]);
    }
}


// =============================================================
// DEVA FORM - ON PLAY
// =============================================================
//
// Completely replaces the original DevaPower effect.
//
// Playing Deva Form now applies our custom DevaPower.
// =============================================================

[HarmonyPatch(
    typeof(DevaForm),
    "OnPlay")]
public static class DevaFormOnPlayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        DevaForm __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0);


        return false;
    }


    private static async Task NewOnPlay(
        DevaForm card,
        PlayerChoiceContext choiceContext)
    {
        await MegaCrit.Sts2.Core.Commands.PowerCmd
            .Apply<DevaPower>(
                choiceContext,
                card.Owner.Creature,
                1,
                card.Owner.Creature,
                card);
    }
}