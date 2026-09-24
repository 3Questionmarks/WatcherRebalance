using BaseLib.Commands;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class ForeseenPower : WatcherRebalancePower
{
    private bool _triggeredThisTurn;
    private CardModel? _foreseenCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task TryTrigger(
        PlayerChoiceContext choiceContext,
        ScryResult result)
    {
        if (_triggeredThisTurn)
            return;

        // Foreseen triggers only when exactly one card was
        // actually discarded by the Scry.
        if (result.Discarded.Count != 1)
            return;

        CardModel card = result.Discarded[0];

        if (!CanForesee(card))
            return;

        // Consume the trigger only after confirming that the
        // Foreseen autoplay can actually occur.
        _triggeredThisTurn = true;
        _foreseenCard = card;

        try
        {
            // null target deliberately lets AutoPlay choose a
            // random valid target for targeted cards.
            await CardCmd.AutoPlay(
                choiceContext,
                card,
                null);
        }
        finally
        {
            _foreseenCard = null;
        }

        // This is deliberately LAST.
        //
        // Sly has already resolved before TryTrigger is called,
        // so Foreseen now gets final control over the card's
        // destination.
        if (card.Pile?.Type != PileType.Exhaust)
        {
            await CardCmd.Exhaust(
                choiceContext,
                card);
        }
    }

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature? target,
        int playCount)
    {
        if (_foreseenCard == null ||
            !ReferenceEquals(card, _foreseenCard))
        {
            return playCount;
        }

        // The normal play count already begins at 1.
        //
        // Foreseen 1 -> 1 Foreseen play
        // Foreseen 2 -> 2 Foreseen plays
        // Foreseen 3 -> 3 Foreseen plays
        return playCount + Math.Max(0, Amount - 1);
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _triggeredThisTurn = false;
            _foreseenCard = null;
        }

        return Task.CompletedTask;
    }

    private bool CanForesee(CardModel card)
    {
        if (card.Keywords.Contains(CardKeyword.Unplayable))
            return false;

        if (card.Owner.Creature.IsDead)
            return false;

        if (CombatManager.Instance.IsOverOrEnding)
            return false;

        var combatState =
            card.CombatState ??
            card.Owner.Creature.CombatState;

        return combatState != null && Hook.ShouldPlay(
            combatState,
            card,
            out _,
            AutoPlayType.Default);
    }
}


// ============================================================================
// SCRY INTEGRATION
// ============================================================================
//
// ScrySlyPatch is also a postfix on this method.
//
// HarmonyAfter makes this postfix run after ScrySlyPatch, giving the order:
//
//   Scry discard
//       -> Sly autoplay
//       -> Foreseen autoplay
//       -> Foreseen Exhaust
//
// This means a Sly card can benefit from both effects, but Foreseen always
// controls the final destination.
// ============================================================================

[HarmonyPatch(typeof(ScryCmd))]
[HarmonyAfter("WatcherRebalance")]
public static class ForeseenScryPatch
{
    [HarmonyPatch(
        nameof(ScryCmd.Execute),
        [
            typeof(PlayerChoiceContext),
            typeof(MegaCrit.Sts2.Core.Entities.Players.Player),
            typeof(int)
        ])]
    [HarmonyPostfix]
    private static async Task<ScryResult> ExecutePostfix(
        Task<ScryResult> __result,
        PlayerChoiceContext __0,
        MegaCrit.Sts2.Core.Entities.Players.Player __1)
    {
        ScryResult result = await __result;

        ForeseenPower? foreseen =
            __1.Creature.GetPower<ForeseenPower>();

        if (foreseen != null)
        {
            await foreseen.TryTrigger(
                __0,
                result);
        }

        return result;
    }
}
