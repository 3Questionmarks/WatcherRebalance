using BaseLib.Hooks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Abstract;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Relics;


[Pool(typeof(WatcherRelicPool))]
public sealed class ShatteredLens :
    WatcherRebalanceRelic,
    IAfterScryed
{
    private const int DamagePerDiscard = 4;


    public override RelicRarity Rarity =>
        RelicRarity.Rare;


    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(BaseLibTip.Scry)
    ];


    public async Task AfterScryed(
        PlayerChoiceContext choiceContext,
        Player player,
        int scryAmount,
        int discardAmount,
        List<CardModel> seen,
        List<CardModel> discarded)
    {
        // Only trigger when this relic's owner performs the Scry.
        if (player != Owner)
            return;


        // Keeping every Scryed card means there is no damage.
        if (discardAmount <= 0)
            return;


        if (Owner.Creature.CombatState == null)
            return;


        decimal damage =
            DamagePerDiscard * discardAmount;


        // Get all living opponents.
        IEnumerable<Creature> enemies =
            Owner.Creature.CombatState
                .GetOpponentsOf(Owner.Creature)
                .Where(enemy => enemy.IsAlive);


        // Materialize once because we need to know whether there
        // is actually anything left to hit.
        List<Creature> targets =
            enemies.ToList();


        if (targets.Count == 0)
            return;


        // Deal the damage simultaneously to all enemies.
        //
        // ValueProp.Unpowered means the relic always deals exactly:
        //
        //     4 × number of discarded cards
        //
        // It is not increased by Strength, Wrath, Divinity, etc.
        await CreatureCmd.Damage(
            choiceContext,
            targets,
            damage,
            ValueProp.Unpowered,
            Owner.Creature);


        Flash();
    }
}