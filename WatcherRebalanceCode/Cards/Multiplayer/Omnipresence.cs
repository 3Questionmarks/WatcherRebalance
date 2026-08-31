using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

[Pool(typeof(WatcherCardPool))]
public sealed class Omnipresence() : WatcherRebalanceCard(
    2,
    CardType.Skill,
    CardRarity.Rare,
    TargetType.None)
{
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;


    // ================================================================
    // UPGRADE
    // ================================================================
    //
    // Cost:
    // 2 -> 1
    //
    // ================================================================

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }


    // ================================================================
    // ON PLAY
    // ================================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState == null)
            return;


        // The Watcher who played Omnipresence is credited as the
        // creator of every generated card.
        Player creator = Owner;


        foreach (Player recipient in CombatState.Players)
        {
            if (!recipient.Creature.IsAlive)
                continue;


            await GiveCard<Miracle>(
                recipient,
                creator);

            await GiveCard<Smite>(
                recipient,
                creator);

            await GiveCard<Insight>(
                recipient,
                creator);
        }
    }


    // ================================================================
    // GIVE GENERATED CARD
    // ================================================================
    //
    // Owner:
    //     the player receiving the card
    //
    // Creator:
    //     the Watcher who played Omnipresence
    //
    // This lets generation hooks such as Master Reality treat the
    // Watcher as the source of every generated card.
    //
    // ================================================================

    private async Task GiveCard<T>(
        Player recipient,
        Player creator)
        where T : CardModel
    {
        if (CombatState == null)
            return;


        CardModel card =
            CombatState.CreateCard(
                ModelDb.Card<T>(),
                recipient);


        await CardPileCmd.AddGeneratedCardToCombat(
            card,
            PileType.Hand,
            creator,
            CardPilePosition.Top);
    }
}