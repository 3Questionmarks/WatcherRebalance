using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

[Pool(typeof(TokenCardPool))]
public sealed class ClaimYourDestiny() : DivineInterventionChoice(CardType.Power)
{
    public override async Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        DivineIntervention source)
    {
        // =====================================================
        // FIND CURRENT COMBAT ROOM
        // =====================================================

        if (source.CombatState?.RunState.CurrentRoom
            is not CombatRoom combatRoom)
        {
            return;
        }


        // =====================================================
        // CREATE ANY-CHARACTER CARD REWARD
        // =====================================================
        //
        // Based on Kaleidoscope's implementation.
        //
        // Instead of using CardCreationOptions.ForRoom(), which
        // restricts the reward to the Watcher's card pool, choose
        // character pools directly.
        //
        // Claim Your Destiny can include the Watcher as well as
        // every other unlocked character.
        // =====================================================

        List<CardPoolModel> characterPools =
            Owner.UnlockState.CharacterCardPools
                .ToList()
                .StableShuffle(Owner.RunState.Rng.Niche)
                .Take(5)
                .ToList();


        List<CardModel> cardsToOffer = new();


        foreach (CardPoolModel cardPool in characterPools)
        {
            CardCreationOptions options =
                new CardCreationOptions(
                    new[] { cardPool },
                    CardCreationSource.Other,
                    CardRarityOddsType.RegularEncounter)
                .WithFlags(
                    CardCreationFlags.NoCardPoolModifications);


            CardModel card =
                CardFactory.CreateForReward(
                        Owner,
                        1,
                        options)
                    .First()
                    .Card;


            cardsToOffer.Add(card);
        }


        // Kaleidoscope uses empty pools for its reroll options.
        // We mirror that behavior here.
        CardCreationOptions rerollOptions =
            CardCreationOptions.ForNonCombatWithDefaultOdds(
                Array.Empty<CardPoolModel>());


        // =====================================================
        // ADD EXTRA CARD REWARD
        // =====================================================

        combatRoom.AddExtraReward(
            Owner,
            new CardReward(
                cardsToOffer,
                CardCreationSource.Other,
                Owner,
                rerollOptions));


        // =====================================================
        // VISUAL INDICATOR
        // =====================================================

        await PowerCmd.Apply<DestinyPower>(
            choiceContext,
            Owner.Creature,
            1M,
            Owner.Creature,
            source);
    }
}