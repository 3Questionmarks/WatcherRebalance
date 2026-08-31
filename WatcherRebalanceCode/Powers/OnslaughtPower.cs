using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class OnslaughtPower : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Single;


    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        Watcher.Code.Core.WatcherHoverTipFactory
            .FromStance<WrathStance>()
    ];


    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player.Creature != Owner)
            return;


        if (player.IsInWatcherStance<WrathStance>())
        {
            await StanceCmd.ExitStance(
                choiceContext,
                player,
                null);
        }


        RemoveInternal();
    }
}