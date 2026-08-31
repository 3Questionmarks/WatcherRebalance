using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;
using Watcher.Code.Events;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;


// =============================================================
// DEVOTION
// =============================================================
//
// Whenever you change Stances,
// add a Miracle into your Hand.
//
// Amount represents the number of Devotions in play.
//
// Example:
//
// Amount 1 -> 1 Miracle
// Amount 2 -> 2 Miracles
// Amount 3 -> 3 Miracles
//
// Hovering the power also previews Miracle.
// =============================================================

public sealed class DevotionPower
    : WatcherRebalancePower,
      IOnStanceChange
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // MIRACLE TOOLTIP
    // =========================================================
    //
    // Hovering Devotion in the power bar will also display
    // the Miracle card preview.
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Miracle>()
    ];


    // =========================================================
    // STANCE CHANGE
    // =========================================================

    public async Task OnStanceChange(
        PlayerChoiceContext ctx,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        if (player.Creature != Owner)
            return;


        // Generate one Miracle for every stack of Devotion.
        for (int i = 0; i < Amount; i++)
        {
            await WatcherCmd.GiveCard<Miracle>(
                player,
                PileType.Hand,
                CardPilePosition.Top,
                skipAnimation: true);
        }


        Flash();
    }
}