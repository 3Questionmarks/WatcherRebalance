using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Anathema : WatcherRebalanceCard
{
    public Anathema()
        : base(
            1,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(
            12m,
            ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory.FromStance<CalmStance>(),
        WatcherHoverTipFactory.FromStance<WrathStance>()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        List<CardModel> cardsToChoose =
        [
            (CardModel)ModelDb
                .Card<CalmCard>()
                .MutableClone(),

            (CardModel)ModelDb
                .Card<WrathCard>()
                .MutableClone()
        ];

        foreach (CardModel card in cardsToChoose)
        {
            card.Owner = Owner;
        }

        CardModel? chosenCard =
            await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                cardsToChoose,
                Owner);

        if (chosenCard is StanceChoiceCard stanceChoice)
        {
            await stanceChoice.ResolveChoice(
                choiceContext,
                this);
        }

        SfxCmd.Play(
            "event:/sfx/characters/regent/regent_select");

        await CommonActions
            .CardAttack(this, cardPlay)
            .WithHitFx(
                "vfx/vfx_dramatic_stab",
                tmpSfx: "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
