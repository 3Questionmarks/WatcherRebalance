using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Core;
using Watcher.Code.Events;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;


// =============================================================
// DEVA FORM
// =============================================================
//
// Whenever you enter:
//
// Calm:
//     Gain Dexterity.
//
// Wrath:
//     Gain Strength.
//
// Divinity:
//     Gain Intangible.
//
// Neutral:
//     Gain Energy.
//
// Amount represents the number of Deva Forms currently active.
// =============================================================

public sealed class DevaPower
    : WatcherRebalancePower,
      IOnStanceChange
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // HOVER TOOLTIPS
    // =========================================================
    //
    // The power itself previews every stance/power referenced
    // by its effect.
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        // Stances
        WatcherHoverTipFactory.FromStance<CalmStance>(),
        WatcherHoverTipFactory.FromStance<WrathStance>(),
        WatcherHoverTipFactory.FromStance<DivinityStance>(),

        // Powers
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<IntangiblePower>()
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


        // =====================================================
        // CALM
        // =====================================================

        if (newStance is CalmStance)
        {
            await PowerCmd.Apply<DexterityPower>(
                ctx,
                Owner,
                Amount,
                Owner,
                null);

            Flash();
            return;
        }


        // =====================================================
        // WRATH
        // =====================================================

        if (newStance is WrathStance)
        {
            await PowerCmd.Apply<StrengthPower>(
                ctx,
                Owner,
                Amount,
                Owner,
                null);

            Flash();
            return;
        }


        // =====================================================
        // DIVINITY
        // =====================================================

        if (newStance is DivinityStance)
        {
            await PowerCmd.Apply<IntangiblePower>(
                ctx,
                Owner,
                Amount,
                Owner,
                null);

            Flash();
            return;
        }


        // =====================================================
        // NEUTRAL
        // =====================================================
        //
        // Neutral is the absence of one of the Watcher's actual
        // stance states.
        //
        // If Calm, Wrath, and Divinity all failed above, this
        // stance change represents entering Neutral.
        // =====================================================

        await PlayerCmd.GainEnergy(
            Amount,
            player);

        Flash();
    }
}