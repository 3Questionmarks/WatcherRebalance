using MegaCrit.Sts2.Core.Entities.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class DestinyPower : WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;

    public override PowerStackType StackType =>
        PowerStackType.Counter;
}