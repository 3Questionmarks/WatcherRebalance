using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Token;
using Watcher.Code.Extensions;
using Watcher.Code.Powers;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch(typeof(Miracle))]
public static class MiraclePatch
{
    /*
     * MIRACLE REBALANCE
     *
     * Base:
     * Retain.
     * Gain 1 Energy.
     * If in Divinity, gain 1 Strength this turn.
     * Exhaust.
     *
     * Upgrade:
     * Energy remains 1.
     * Temporary Strength becomes 2.
     * Gain 2 Mantra.
     *
     * Tooltips:
     * - Divinity always shown.
     * - Strength always shown.
     * - Mantra only shown on Miracle+.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withVars = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithVars",
            new[] { typeof(DynamicVar[]) }
        );

        MethodInfo? replacement = AccessTools.Method(
            typeof(MiraclePatch),
            nameof(ReplaceMiracleVars)
        );

        if (withVars == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithVars."
            );
        }

        if (replacement == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find MiraclePatch.ReplaceMiracleVars."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withVars))
                continue;

            var newInstruction = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replacement
            );

            newInstruction.labels.AddRange(code[i].labels);
            newInstruction.blocks.AddRange(code[i].blocks);

            code[i] = newInstruction;

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to find Miracle's WithVars call."
            );
        }

        return code;
    }


    private static ConstructedCardModel ReplaceMiracleVars(
        ConstructedCardModel card,
        DynamicVar[] originalVars)
    {
        /*
         * Replace Miracle's original:
         *
         * Energy 1 -> 2
         *
         * with:
         *
         * Energy:   1 -> 1
         * Strength: 1 -> 2
         *
         * Mantra is added separately below so we can suppress
         * its automatic tooltip on the base version.
         */

        DynamicVar[] vars =
        {
            new EnergyVar(1),

            new PowerVar<StrengthPower>(1)
                .WithUpgrade(1)
        };


        // -----------------------------------------------------
        // Add Energy + Strength through BaseLib's real WithVars.
        // -----------------------------------------------------

        MethodInfo? withVars = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithVars",
            new[] { typeof(DynamicVar[]) }
        );

        if (withVars == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithVars."
            );
        }

        object? result = withVars.Invoke(
            card,
            new object[] { vars }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: ConstructedCardModel.WithVars returned an unexpected result."
            );
        }


        // -----------------------------------------------------
        // Add Mantra 0 -> 2 WITHOUT its automatic tooltip.
        //
        // WatcherCardModel has:
        //
        // WithPower<T>(int baseVal, int upgrade, bool showTooltip)
        //
        // We deliberately pass false.
        // -----------------------------------------------------

        MethodInfo? watcherWithPower = typeof(WatcherCardModel)
            .GetMethods(
                BindingFlags.Instance |
                BindingFlags.NonPublic
            )
            .FirstOrDefault(m =>
                m.Name == "WithPower" &&
                m.IsGenericMethodDefinition &&
                m.GetParameters().Length == 3 &&
                m.GetParameters()[0].ParameterType == typeof(int) &&
                m.GetParameters()[1].ParameterType == typeof(int) &&
                m.GetParameters()[2].ParameterType == typeof(bool)
            );

        if (watcherWithPower == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find WatcherCardModel.WithPower(int, int, bool)."
            );
        }

        watcherWithPower
            .MakeGenericMethod(typeof(MantraPower))
            .Invoke(
                card,
                new object[]
                {
                    0, // Base Miracle
                    2, // Miracle+
                    false // No automatic tooltip
                }
            );


        // -----------------------------------------------------
        // Add Mantra tooltip ONLY when the card is upgraded.
        //
        // BaseLib's WithTips callback is evaluated against the
        // actual card instance, so we can check IsUpgraded.
        // -----------------------------------------------------

        MethodInfo? withTips = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithTips",
            new[]
            {
                typeof(Func<CardModel, IEnumerable<IHoverTip>>)
            }
        );

        if (withTips == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithTips."
            );
        }

        Func<CardModel, IEnumerable<IHoverTip>> mantraTooltip =
            model =>
            {
                if (!model.IsUpgraded)
                    return Array.Empty<IHoverTip>();

                return new IHoverTip[]
                {
                    HoverTipFactory.FromPower<MantraPower>(
                        model.DynamicVars
                            .Power<MantraPower>()
                            .IntValue
                    )
                };
            };

        withTips.Invoke(
            card,
            new object[]
            {
                mantraTooltip
            }
        );


        // Add the shared Divine keyword-style tooltip.
        WatcherRebalanceTips.AddDivineTip(card);

        return constructedCard;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        Miracle __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedMiracle(
            __instance,
            choiceContext,
            cardPlay
        );

        return false;
    }

    // Glow if in Divinity
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not Miracle card)
            return;

        __result =
            card.Owner
                .IsInWatcherStance<DivinityStance>();
    }


    private static async Task PlayRebalancedMiracle(
        Miracle card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // -----------------------------------------------------
        // Miracle+ gains 2 Mantra first.
        //
        // This means that if the Mantra gain causes us to enter
        // Divinity, the Strength effect below will also trigger.
        //
        // Base Miracle has a value of 0, so nothing is applied.
        // -----------------------------------------------------

        int mantraAmount =
            card.DynamicVars
                .Power<MantraPower>()
                .IntValue;

        if (mantraAmount > 0)
        {
            await CommonActions.ApplySelf<MantraPower>(
                ctx,
                card
            );
        }


        // -----------------------------------------------------
        // Gain 1 Energy.
        // -----------------------------------------------------

        await PlayerCmd.GainEnergy(
            card.DynamicVars.Energy.IntValue,
            card.Owner
        );


        // -----------------------------------------------------
        // If now in Divinity:
        //
        // Gain 1 Strength this turn.
        // Miracle+: Gain 2 Strength this turn.
        //
        // Because Mantra was applied first, Miracle+ can now
        // trigger this effect by entering Divinity itself.
        // -----------------------------------------------------

        bool isInDivinity =
            card.Owner.IsInWatcherStance<DivinityStance>();

        if (isInDivinity)
        {
            int strengthAmount =
                card.DynamicVars
                    .Power<StrengthPower>()
                    .IntValue;

            if (strengthAmount > 0)
            {
                await PowerCmd.Apply<MiraclePower>(
                    ctx,
                    card.Owner.Creature,
                    strengthAmount,
                    card.Owner.Creature,
                    card
                );
            }
        }
    }
}
