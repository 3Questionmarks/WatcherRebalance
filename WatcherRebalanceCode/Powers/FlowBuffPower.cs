using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using WatcherRebalance.WatcherRebalanceCode.Patches;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

/// <summary>
/// Temporary Flow accumulated during the current turn.
///
/// Each stack:
///   +1 damage to Attacks
///   +1 Block from cards
///
/// The damage contribution is divided by the current Watcher stance
/// multiplier before the game's normal stance multiplier is applied.
/// This makes Flow post-multiplier:
///
///   6 damage + 3 Flow:
///       Neutral  = 9
///       Wrath x2 = 15
///       Divinity x3 = 21
/// </summary>
public sealed class FlowBuffPower : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // ========================================================================
    // ATTACK DAMAGE
    // ========================================================================

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Amount <= 0)
            return 0m;

        if (dealer != Owner)
            return 0m;

        // Flow increases Attacks, not arbitrary damage effects.
        if (cardSource == null ||
            cardSource.Type != CardType.Attack ||
            cardSource.Owner.Creature != Owner)
        {
            return 0m;
        }

        decimal flowDamage = Amount;

        Player? player = Owner.Player;

        if (player == null)
            return flowDamage;

        decimal stanceMultiplier =
            StanceConfigMath.GetConfiguredOutgoingMultiplier(
                player,
                Owner.CombatState);

        if (stanceMultiplier == 0m ||
            stanceMultiplier == 1m)
        {
            return flowDamage;
        }

        // The normal damage pipeline applies the stance multiplier later.
        // Divide here so Flow itself remains +1 per stack after that step.
        return flowDamage / stanceMultiplier;
    }


    // ========================================================================
    // CARD BLOCK
    // ========================================================================

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Amount <= 0)
            return 0m;

        // Flow only improves Block originating from one of the owner's cards.
        if (cardSource == null ||
            cardSource.Owner.Creature != Owner)
        {
            return 0m;
        }

        if (!props.IsPoweredCardOrMonsterMoveBlock())
            return 0m;

        return Amount;
    }


    // ========================================================================
    // TURN RESET
    // ========================================================================

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player)
            return;

        await PowerCmd.Remove(this);
    }
}
