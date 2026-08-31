using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(FollowUp))]
public static class FollowUpPatch
{
    /*
     * FOLLOW-UP REBALANCE
     *
     * Deal 8 damage.
     * If the last card played was an Attack,
     * gain 1 (2) Energy.
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

        MethodInfo? withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? withEnergy = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithEnergy",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        MethodInfo? replaceDamage = AccessTools.Method(
            typeof(FollowUpPatch),
            nameof(ReplaceDamage)
        );

        MethodInfo? replaceEnergy = AccessTools.Method(
            typeof(FollowUpPatch),
            nameof(ReplaceEnergy)
        );

        if (withDamage == null ||
            withEnergy == null ||
            replaceDamage == null ||
            replaceEnergy == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find Follow-Up constructor methods."
            );
        }

        bool damagePatched = false;
        bool energyPatched = false;

        for (int i = 0; i < code.Count; i++)
        {
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

            if (!energyPatched && code[i].Calls(withEnergy))
            {
                var replacement = new CodeInstruction(
                    System.Reflection.Emit.OpCodes.Call,
                    replaceEnergy
                );

                replacement.labels.AddRange(code[i].labels);
                replacement.blocks.AddRange(code[i].blocks);

                code[i] = replacement;
                energyPatched = true;
            }
        }

        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Follow-Up damage."
            );
        }

        if (!energyPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Follow-Up Energy."
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
                8,
                0
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: Could not replace Follow-Up damage."
            );
        }

        return constructedCard;
    }


    private static ConstructedCardModel ReplaceEnergy(
        ConstructedCardModel card,
        int originalBase,
        int originalUpgrade)
    {
        MethodInfo? withEnergy = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithEnergy",
            new[]
            {
                typeof(int),
                typeof(int)
            }
        );

        object? result = withEnergy?.Invoke(
            card,
            new object[]
            {
                1,
                1
            }
        );

        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: Could not replace Follow-Up Energy."
            );
        }

        return constructedCard;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    public static bool OnPlayPrefix(
        FollowUp __instance,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = PlayRebalancedFollowUp(
            __instance,
            choiceContext,
            cardPlay
        );

        return false;
    }


    private static async Task PlayRebalancedFollowUp(
        FollowUp card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        await CommonActions.CardAttack(
                card,
                cardPlay
            )
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        /*
         * We cannot directly access Follow-Up's private
         * WasLastCardPlayedAttack property, so reproduce
         * the same history check here.
         */

        var lastCardEntry =
            MegaCrit.Sts2.Core.Combat.CombatManager
                .Instance
                .History
                .CardPlaysStarted
                .LastOrDefault(e =>
                    e.CardPlay.Card.Owner == card.Owner &&
                    e.CardPlay.Card != card
                );

        if (lastCardEntry == null)
            return;

        if (lastCardEntry.CardPlay.Card.Type != CardType.Attack)
            return;

        card.Owner.PlayerCombatState!.GainEnergy(
            card.DynamicVars.Energy.IntValue
        );
    }
}