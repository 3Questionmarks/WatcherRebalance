using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Cards.Token;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class SmitePower :
    WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Debuff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Smite>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];


    // =========================================================
    // APPLY
    // =========================================================

    public override async Task BeforeApplied(
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // Immediately remove Strength.
        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            target,
            -amount,
            applier,
            cardSource,
            true);
    }


    // =========================================================
    // STACKING / AMOUNT CHANGES
    // =========================================================

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this)
            return;

        if (amount == Amount)
            return;

        // If the amount of this temporary debuff changes,
        // adjust Strength by the same difference.
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            -amount,
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
        // Only expire when the affected creature's side ends
        // its turn.
        if (!participants.Contains(Owner))
            return;

        Flash();

        int strengthToRestore =
            Amount;

        await PowerCmd.Remove(this);

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner,
            strengthToRestore,
            Owner,
            null,
            true);
    }
}