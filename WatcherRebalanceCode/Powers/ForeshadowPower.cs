using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class ForeshadowPower
    : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // DOUBLE CARD PLAY
    // =========================================================

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature? target,
        int playCount)
    {
        // Only affect cards belonging to this power's owner.
        if (card.Owner.Creature != Owner)
        {
            return playCount;
        }


        // Play the card one additional time.
        return playCount + 1;
    }


    // =========================================================
    // CONSUME ONE STACK
    // =========================================================

    public override async Task AfterModifyingCardPlayCount(
        CardModel card)
    {
        // Burst uses this hook to consume one stack after
        // granting the additional play.
        //
        // Keep the ownership guard because Forshadow affects
        // all card types.

        if (card.Owner.Creature != Owner)
        {
            return;
        }


        await PowerCmd.Decrement(this);
    }


    // =========================================================
    // EXPIRE AT END OF TURN
    // =========================================================

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }


        // Any unused Forshadow stacks disappear at the end
        // of the turn they were granted for.
        await PowerCmd.Remove(this);
    }
}