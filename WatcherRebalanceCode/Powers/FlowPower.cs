using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Events;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class FlowPower :
    WatcherRebalancePower,
    IOnStanceChange
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // STANCE CHANGE
    // =========================================================
    //
    // Watcher's stance system automatically dispatches
    // IOnStanceChange whenever the player genuinely changes
    // Stances.
    //
    // Each stack of this power grants 1 Flow whenever that
    // happens.
    //
    // Examples:
    //
    //     1 FlowPower:
    //         Change Stance -> gain 1 Flow
    //
    //     2 FlowPower:
    //         Change Stance -> gain 2 Flow
    //
    // =========================================================

    public async Task OnStanceChange(
        PlayerChoiceContext ctx,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        // Only respond to stance changes made by this
        // power's owner.
        if (player.Creature != Owner)
            return;


        // Gain temporary Flow equal to the number of
        // FlowPower stacks.
        await PowerCmd.Apply<FlowBuffPower>(
            ctx,
            Owner,
            Amount,
            Owner,
            null);
    }
}