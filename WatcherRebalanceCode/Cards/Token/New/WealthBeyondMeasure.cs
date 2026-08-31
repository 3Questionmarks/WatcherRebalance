using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

[Pool(typeof(TokenCardPool))]
public sealed class WealthBeyondMeasure
    : DivineInterventionChoice
{
    private const int GoldAmount = 25;


    public WealthBeyondMeasure()
        : base(CardType.Power)
    {
    }


    public override async Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        DivineIntervention source)
    {
        VfxCmd.PlayOnCreature(
            Owner.Creature,
            "vfx/vfx_coin_explosion_regular");


        await PlayerCmd.GainGold(
            GoldAmount,
            Owner);
    }
}