using BaseLib.Commands;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class ManipulateRealityPower
    : WatcherRebalancePower
{
    // ================================================================
    // POWER SETUP
    // ================================================================

    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Single;


    // ================================================================
    // IMMEDIATE RESOLUTION
    // ================================================================
    //
    // AfterPowerAmountChanged gives us a real PlayerChoiceContext.
    //
    // This is important because Scry opens a player choice and cannot
    // safely be performed from AfterApplied(), which has no context.
    //
    // Each copy of this power belongs to the player who needs to
    // perform the Scry.
    //
    // ================================================================

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        // This hook can run when other powers change too.
        // Only respond to this specific instance.
        if (power != this)
            return;


        // We only care about gaining the power.
        if (amount <= 0)
            return;


        // The power is attached directly to the affected player's
        // creature, so Owner.Player is the player who should make
        // the choice.
        var player =
            Owner.Player;


        if (player == null ||
            !Owner.IsAlive)
        {
            RemoveInternal();
            return;
        }


        int scryAmount =
            Amount;


        // ============================================================
        // SCRY
        // ============================================================
        //
        // This now originates from the power attached to THIS
        // player's creature instead of Manipulate Reality trying
        // to control another player's piles from the card.
        //
        // ============================================================

        await ScryCmd.Execute(
            choiceContext,
            player,
            scryAmount);


        // Player may theoretically no longer be alive after the
        // choice resolves.
        if (Owner.IsAlive)
        {
            // ========================================================
            // DRAW 1
            // ========================================================

            await CardPileCmd.Draw(
                choiceContext,
                1,
                player);
        }


        // ============================================================
        // REMOVE THE TEMPORARY POWER
        // ============================================================
        //
        // It has completed its one-shot job.
        //
        // ============================================================

        RemoveInternal();
    }
}