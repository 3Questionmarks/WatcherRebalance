using BaseLib.Commands;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class ManipulateRealityPower
    : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Single;


    // ================================================================
    // RESOLVE THIS PLAYER'S COPY
    // ================================================================
    //
    // The card decides which PlayerChoiceContext this receives:
    //
    // CARD OWNER:
    //     The original PlayCardAction context.
    //
    // OTHER PLAYERS:
    //     Their own HookPlayerChoiceContext.
    //
    // This lets all players have independent multiplayer choices
    // without creating a hook action behind the card owner's own
    // currently-running PlayCardAction.
    // ================================================================

    public async Task Resolve(
        PlayerChoiceContext choiceContext)
    {
        Player? player =
            Owner.Player;


        if (player == null ||
            !Owner.IsAlive)
        {
            await PowerCmd.Remove(this);
            return;
        }


        int scryAmount =
            Amount;


        // ============================================================
        // SCRY
        // ============================================================

        await ScryCmd.Execute(
            choiceContext,
            player,
            scryAmount);


        // ============================================================
        // DRAW 1
        // ============================================================

        if (Owner.IsAlive)
        {
            await CardPileCmd.Draw(
                choiceContext,
                1,
                player);
        }


        // ============================================================
        // CONSUME POWER
        // ============================================================
        //
        // Use the proper command rather than RemoveInternal().
        //
        // This keeps the removal in the synchronized game-state
        // command flow on every peer.
        // ============================================================

        await PowerCmd.Remove(this);
    }
}