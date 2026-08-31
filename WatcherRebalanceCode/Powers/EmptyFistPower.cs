using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class EmptyFistPower
    : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // TOOLTIPS
    // =========================================================
    //
    // Same idea as MiraclePower, except using the
    // Empty Fist card tooltip as requested.
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<EmptyFist>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];


    // =========================================================
    // WHEN FIRST APPLIED
    // =========================================================

    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // Immediately grant the actual Strength.
        //
        // EmptyFistPower acts as the temporary marker which
        // remembers how much Strength must later be removed.

        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            applier,
            cardSource,
            true);
    }


    // =========================================================
    // WHEN STACKED
    // =========================================================

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount == Amount ||
            power != this)
        {
            return;
        }


        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            amount,
            applier,
            cardSource,
            true);
    }


    // =========================================================
    // END OF TURN
    // =========================================================

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;


        Flash();


        // Remove the temporary marker first.

        await PowerCmd.Remove(this);


        // Then remove exactly the Strength it granted.

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            -Amount,
            Owner,
            null);
    }
}