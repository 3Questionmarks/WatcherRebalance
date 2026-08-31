using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Token;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public class MiraclePower : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;
    
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Miracle>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];


    // =========================================================
    // WHEN APPLIED
    // =========================================================

    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // Immediately gain the same amount of real Strength.
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            amount,
            applier,
            cardSource,
            true
        );
    }


    // =========================================================
    // WHEN THE POWER STACKS
    // =========================================================

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // This matches TemporaryStrengthPower's handling.
        //
        // "amount" here is the amount of Strength that needs
        // to be added as a result of the power amount changing.
        if (amount == Amount || power != this)
            return;

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            amount,
            applier,
            cardSource,
            true
        );
    }


    // =========================================================
    // END OF TURN
    // =========================================================

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // Only expire when our owner's side ends its turn.
        if (!participants.Contains(Owner))
            return;

        Flash();

        // Remove the temporary-power marker first.
        await PowerCmd.Remove(this);

        // Then remove the Strength it granted.
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            -Amount,
            Owner,
            null
        );
    }
}