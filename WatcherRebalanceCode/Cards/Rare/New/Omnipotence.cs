using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Watcher.Code.Character;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Omnipotence : WatcherRebalanceCard
{
    public Omnipotence()
        : base(
            0,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }


    // =========================================================
    // X COST
    // =========================================================

    protected override bool HasEnergyCostX => true;


    // =========================================================
    // VARIABLES
    // =========================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
            .WithUpgrade(1)
    ];


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<MantraPower>()
    ];


    // =========================================================
    // KEYWORDS
    // =========================================================

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];


    // =========================================================
    // PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue();


        // -----------------------------------------------------
        // DRAW X CARDS
        // -----------------------------------------------------

        if (x > 0)
        {
            await CardPileCmd.Draw(
                choiceContext,
                x,
                Owner);
        }


        // -----------------------------------------------------
        // GAIN X * 2 MANTRA
        // -----------------------------------------------------

        int mantraAmount = x * 2;

        if (mantraAmount > 0)
        {
            await PowerCmd.Apply<MantraPower>(
                choiceContext,
                Owner.Creature,
                mantraAmount,
                Owner.Creature,
                this);
        }


        // -----------------------------------------------------
        // GAIN 1 / 2 ENERGY
        // -----------------------------------------------------

        await PlayerCmd.GainEnergy(
            DynamicVars.Energy.IntValue,
            Owner);
    }
}