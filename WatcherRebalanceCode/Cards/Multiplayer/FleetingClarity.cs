using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Multiplayer;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Powers;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;


// ============================================================================
// FLEETING CLARITY
// ============================================================================
//
// Multiplayer Uncommon Skill
// Cost: 1
//
// Another player enters Calm.
// If you are in Calm, all other players enter Calm.
// At the start of the next turn they exit Calm.
// Exhaust.
//
// Upgrade:
// Remove Exhaust.
//
// ============================================================================

[HarmonyPatch(typeof(FleetingClarity))]
public static class FleetingClarityPatch
{
    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================
    //
    // Original:
    //
    // Cost: 2
    // Target: AllAllies
    //
    // New:
    //
    // Cost: 1
    // Target: AnyAlly
    //
    // We deliberately leave the original keyword call alone:
    //
    // WithKeyword(CardKeyword.Exhaust, UpgradeType.Remove);
    //
    // so Exhaust is removed on upgrade exactly as in the original card.
    //
    // ========================================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        ConstructorInfo? watcherConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);


        if (watcherConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor ||
                constructor != watcherConstructor)
            {
                continue;
            }


            // ----------------------------------------------------------------
            // Cost:
            //
            // 2 -> 1
            // ----------------------------------------------------------------

            ReplaceInt(
                code,
                i - 5,
                1);


            // ----------------------------------------------------------------
            // Target:
            //
            // AllAllies -> AnyAlly
            // ----------------------------------------------------------------

            ReplaceInt(
                code,
                i - 2,
                (int)TargetType.AnyAlly);


            break;
        }


        return code;
    }


    // ========================================================================
    // ON PLAY
    // ========================================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        FleetingClarity __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0,
                __1);

        return false;
    }


    private static async Task NewOnPlay(
        FleetingClarity card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        if (card.CombatState == null)
        {
            return;
        }


        List<Creature> targets;


        // ====================================================================
        // OWNER IS IN CALM
        // ====================================================================
        //
        // Affect every OTHER living player.
        //
        // ====================================================================

        if (card.Owner.IsInWatcherStance<CalmStance>())
        {
            targets =
                card.CombatState
                    .PlayerCreatures
                    .Where(creature =>
                        creature.IsAlive &&
                        creature != card.Owner.Creature)
                    .ToList();
        }


        // ====================================================================
        // OWNER IS NOT IN CALM
        // ====================================================================
        //
        // Affect only the selected other player.
        //
        // ====================================================================

        else
        {
            Creature? target =
                cardPlay.Target;


            if (target == null ||
                !target.IsAlive ||
                target == card.Owner.Creature ||
                target.Player == null)
            {
                return;
            }


            targets =
            [
                target
            ];
        }


        if (targets.Count == 0)
        {
            return;
        }


        // ====================================================================
        // APPLY DELAYED STANCE-EXIT POWER
        // ====================================================================
        //
        // MultiplayerCardUncommonPower waits until the affected player's
        // next hand draw, exits Calm if they are still in Calm, then removes
        // itself.
        //
        // ====================================================================

        await CommonActions.Apply<MultiplayerCardUncommonPower>(
            ctx,
            targets,
            card,
            true);


        // ====================================================================
        // ENTER CALM
        // ====================================================================

        foreach (Creature creature in targets)
        {
            if (creature.Player == null)
            {
                continue;
            }


            await StanceCmd.EnterCalm(
                ctx,
                creature.Player,
                card);
        }
    }


    // ========================================================================
    // GOLD GLOW
    // ========================================================================
    //
    // Glow while the owner is in Calm, because that is when Fleeting Clarity
    // changes from affecting one selected teammate to affecting all other
    // players.
    //
    // ========================================================================

    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
        {
            return;
        }


        if (__instance is not FleetingClarity card)
        {
            return;
        }


        __result =
            card.Owner.IsInWatcherStance<CalmStance>();
    }


    // ========================================================================
    // IL HELPER
    // ========================================================================

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


        code[index] = replacement;
    }
}