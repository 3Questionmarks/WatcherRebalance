using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class ForeshadowingPower
    : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // END OF CURRENT TURN
    // =========================================================

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // Only trigger when this power's owner has just
        // finished their turn.
        if (!participants.Contains(Owner))
        {
            return;
        }


        // Convert the pending amount into the actual
        // next-turn duplication power.
        await PowerCmd.Apply<ForeshadowPower>(
            choiceContext,
            Owner,
            Amount,
            Owner,
            null);


        // The pending power has done its job.
        await PowerCmd.Remove(this);
    }
}