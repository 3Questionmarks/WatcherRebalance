using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

[Pool(typeof(TokenCardPool))]
public sealed class AttainPerfection
    : DivineInterventionChoice
{
    private const int EnchantmentAmount = 2;


    public AttainPerfection()
        : base(CardType.Power)
    {
    }
    
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CreateEnchantmentTip<Sharp>(),
        CreateEnchantmentTip<Nimble>(),
        CreateEnchantmentTip<Swift>()
    ];


    private static IHoverTip CreateEnchantmentTip<T>()
        where T : EnchantmentModel
    {
        EnchantmentModel enchantment =
            ModelDb.Enchantment<T>().ToMutable();

        enchantment.Amount = EnchantmentAmount;

        return enchantment.HoverTip;
    }

    public override Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        DivineIntervention source)
    {
        // =====================================================
        // FIND ELIGIBLE PERMANENT-DECK CARDS
        // =====================================================
        //
        // This operates directly on Owner.Deck, NOT on the
        // combat deck.
        //
        // The enchantment therefore becomes a permanent deck
        // modification and will appear from the next combat
        // onward.
        //
        // Already-enchanted cards are excluded before the
        // random selection.
        // =====================================================

        List<CardModel> eligibleCards =
            Owner.Deck.Cards
                .Where(IsEligible)
                .ToList();


        // If every card is already enchanted or otherwise
        // ineligible, this choice simply does nothing.
        if (eligibleCards.Count == 0)
        {
            return Task.CompletedTask;
        }


        // =====================================================
        // CHOOSE RANDOM ELIGIBLE CARD
        // =====================================================

        CardModel? card =
            Owner.RunState
                .Rng
                .Niche
                .NextItem(eligibleCards);


        if (card == null)
        {
            return Task.CompletedTask;
        }


        // =====================================================
        // ENCHANT BASED ON CARD TYPE
        // =====================================================
        //
        // Attack -> Sharp 2
        // Skill  -> Nimble 2
        // Power  -> Swift 2
        // =====================================================

        switch (card.Type)
        {
            case CardType.Attack:

                CardCmd.Enchant<Sharp>(
                    card,
                    EnchantmentAmount);

                break;


            case CardType.Skill:

                CardCmd.Enchant<Nimble>(
                    card,
                    EnchantmentAmount);

                break;


            case CardType.Power:

                CardCmd.Enchant<Swift>(
                    card,
                    EnchantmentAmount);

                break;


            default:

                return Task.CompletedTask;
        }


        // =====================================================
        // SHOW ENCHANTED CARD
        // =====================================================
        //
        // IMPORTANT:
        //
        // This happens AFTER CardCmd.Enchant.
        //
        // NCardEnchantVfx reads card.Enchantment when it
        // initializes, so the preview displays the newly-added
        // Sharp / Nimble / Swift enchantment and its amount.
        //
        // The VFX also plays the native enchant shimmer SFX.
        // =====================================================

        NCardEnchantVfx? enchantVfx =
            NCardEnchantVfx.Create(card);

        if (enchantVfx != null)
        {
            NRun.Instance?
                .GlobalUi
                .CardPreviewContainer
                .AddChildSafely(enchantVfx);
        }


        return Task.CompletedTask;
    }


    // =========================================================
    // ELIGIBILITY
    // =========================================================

    private static bool IsEligible(
        CardModel card)
    {
        // -----------------------------------------------------
        // ALREADY ENCHANTED
        // -----------------------------------------------------

        if (card.Enchantment != null)
        {
            return false;
        }


        // -----------------------------------------------------
        // ATTACK -> SHARP
        // -----------------------------------------------------

        if (card.Type == CardType.Attack)
        {
            return ModelDb
                .Enchantment<Sharp>()
                .CanEnchant(card);
        }


        // -----------------------------------------------------
        // SKILL -> NIMBLE
        // -----------------------------------------------------
        //
        // CanEnchant naturally excludes Skills for which Nimble
        // isn't applicable, such as Skills without Block.
        // -----------------------------------------------------

        if (card.Type == CardType.Skill)
        {
            return ModelDb
                .Enchantment<Nimble>()
                .CanEnchant(card);
        }


        // -----------------------------------------------------
        // POWER -> SWIFT
        // -----------------------------------------------------

        if (card.Type == CardType.Power)
        {
            return ModelDb
                .Enchantment<Swift>()
                .CanEnchant(card);
        }


        return false;
    }
}