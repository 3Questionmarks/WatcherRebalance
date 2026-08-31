using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Cards.Common;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(Prostrate))]
public static class ProstratePatch
{
    /*
     * PROSTRATE REBALANCE
     *
     * Gain 2 (3) Mantra.
     * Gain 2 (3) Block.
     *
     * Gain additional Block equal to half
     * the Mantra gained this combat.
     *
     * Half Mantra is always rounded down.
     *
     * The displayed Block includes the Mantra
     * that this Prostrate is about to grant.
     */


    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Original:
    //
    // WithBlock(4);
    // WithPower<MantraPower>(2, 1);
    //
    // We replace only WithBlock(4).
    //
    // The existing Mantra variable stays untouched:
    //
    // 2 Mantra
    // 3 Mantra upgraded
    //

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        MethodInfo? withBlock = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithBlock",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? replacement = AccessTools.Method(
            typeof(ProstratePatch),
            nameof(ReplaceBlock)
        );

        if (withBlock == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithBlock."
            );
        }

        if (replacement == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ProstratePatch.ReplaceBlock."
            );
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withBlock))
                continue;

            var newInstruction = new CodeInstruction(
                System.Reflection.Emit.OpCodes.Call,
                replacement
            );

            newInstruction.labels.AddRange(
                code[i].labels
            );

            newInstruction.blocks.AddRange(
                code[i].blocks
            );

            code[i] = newInstruction;

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Prostrate Block."
            );
        }

        return code;
    }


    // =========================================================
    // REPLACE BLOCK VARIABLE
    // =========================================================

    private static ConstructedCardModel ReplaceBlock(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        /*
         * We replace the original:
         *
         * WithBlock(4)
         *
         * with:
         *
         * Base Block:     2
         * Upgrade:       +1
         * Calculated:    + floor(total Mantra / 2)
         *
         * Therefore:
         *
         * Prostrate:
         * 2 Block + half Mantra
         *
         * Prostrate+:
         * 3 Block + half Mantra
         */

        MethodInfo? withCalculatedBlock =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic
                )
                .FirstOrDefault(m =>
                {
                    if (m.Name != "WithCalculatedBlock")
                        return false;

                    ParameterInfo[] parameters =
                        m.GetParameters();

                    return
                        parameters.Length == 5 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType ==
                            typeof(Func<CardModel, Creature?, decimal>) &&
                        parameters[2].ParameterType == typeof(ValueProp) &&
                        parameters[3].ParameterType == typeof(int) &&
                        parameters[4].ParameterType == typeof(int);
                });

        if (withCalculatedBlock == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCalculatedBlock."
            );
        }

        Func<CardModel, Creature?, decimal> bonus =
            MantraBlockBonus;

        object? result = withCalculatedBlock.Invoke(
            card,
            new object[]
            {
                2,              // Base Block
                bonus,          // Additional Block calculation
                ValueProp.Move,
                1,              // Upgrade: 2 -> 3
                0
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: WithCalculatedBlock returned an unexpected result."
            );
        }

        return constructedCard;
    }


    // =========================================================
    // MANTRA HISTORY
    // =========================================================
    //
    // This is the same basic history check used by Brilliance.
    //

    private static decimal MantraGainedThisCombat(
        CardModel card)
    {
        return CombatManager
            .Instance
            .History
            .Entries
            .OfType<PowerReceivedEntry>()
            .Where(e =>
                e.Power is MantraPower &&
                e.Applier?.Player == card.Owner &&
                e.Amount > 0
            )
            .Sum(e => e.Amount);
    }


    // =========================================================
    // CALCULATED BLOCK
    // =========================================================

    private static decimal MantraBlockBonus(
        CardModel card,
        Creature? creature)
    {
        /*
         * Mantra already recorded in combat history.
         */
        decimal mantraAlreadyGained =
            MantraGainedThisCombat(card);


        /*
         * Also include the Mantra this Prostrate
         * is ABOUT TO grant.
         *
         * Base Prostrate:     +2
         * Upgraded Prostrate: +3
         *
         * This fixes the card preview / tooltip so the
         * displayed Block reflects the result of actually
         * playing the card.
         */
        decimal mantraFromThisCard =
            card.DynamicVars
                .Power<MantraPower>()
                .IntValue;


        decimal totalMantra =
            mantraAlreadyGained +
            mantraFromThisCard;


        /*
         * Always round down.
         *
         * 1 Mantra -> 0 Block
         * 2 Mantra -> 1 Block
         * 3 Mantra -> 1 Block
         * 4 Mantra -> 2 Block
         * 5 Mantra -> 2 Block
         */
        return decimal.Floor(
            totalMantra / 2m
        );
    }


    // =========================================================
    // ON PLAY
    // =========================================================
    //
    // Important:
    //
    // The calculated tooltip above already includes the
    // Mantra this Prostrate is ABOUT TO gain.
    //
    // If we simply allowed the original OnPlay to run:
    //
    // 1. Gain Mantra
    // 2. Recalculate Block
    //
    // then that new Mantra would exist in combat history AND
    // we'd add mantraFromThisCard again.
    //
    // That would count Prostrate's own Mantra twice.
    //
    // Instead, capture the correct Block amount BEFORE applying
    // Mantra, then grant exactly that amount afterward.
    //

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        Prostrate __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedProstrate(
            __instance,
            ctx,
            cardPlay
        );

        return false;
    }


    private static async Task PlayRebalancedProstrate(
        Prostrate card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        /*
         * Mantra that has already been gained before
         * this Prostrate resolves.
         */
        decimal mantraAlreadyGained =
            MantraGainedThisCombat(card);

        /*
         * Mantra this Prostrate is about to grant:
         *
         * Base:     2
         * Upgraded: 3
         */
        int mantraFromThisCard =
            card.DynamicVars
                .Power<MantraPower>()
                .IntValue;

        /*
         * Base Block printed on the card:
         *
         * Base:     2
         * Upgraded: 3
         */
        int baseBlock =
            card.IsUpgraded
                ? 3
                : 2;

        /*
         * Additional Block =
         * half of all Mantra gained this combat,
         * including this Prostrate's Mantra.
         *
         * Always round down.
         */
        int bonusBlock =
            (int)decimal.Floor(
                (mantraAlreadyGained + mantraFromThisCard) / 2m
            );

        int totalBlock =
            baseBlock + bonusBlock;

        /*
         * Preserve the card's effect order:
         *
         * 1. Gain Mantra
         * 2. Gain Block
         */
        await CommonActions.ApplySelf<MantraPower>(
            ctx,
            card
        );

        await CreatureCmd.GainBlock(
            card.Owner.Creature,
            totalBlock,
            ValueProp.Move,
            cardPlay
        );
    }
}