using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using Watcher.Code.Character;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon.New;


[Pool(typeof(WatcherCardPool))]
public sealed class Revere() :
    WatcherRebalanceCard(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherRebalanceTips.Token(),
        WatcherRebalanceTips.Enchant(),
        ..HoverTipFactory.FromEnchantment<Spiral>()
    ];

    protected override Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        List<CardModel> tokenCards =
            PileType.Hand
                .GetPile(Owner)
                .Cards
                .Where(card =>
                    card.Rarity == CardRarity.Token &&
                    card.Enchantment == null)
                .ToList();

        try
        {
            SpiralTokenEnchantPatch.AllowTokenSpiral = true;

            foreach (CardModel tokenCard in tokenCards)
            {
                CardCmd.Enchant<Spiral>(
                    tokenCard,
                    1M);
            }
        }
        finally
        {
            SpiralTokenEnchantPatch.AllowTokenSpiral = false;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}


/// <summary>
/// Spiral normally only accepts Basic Strike/Defend cards.
///
/// While Revere is resolving, Token cards may instead receive Spiral.
/// Cards that already have an Enchantment remain ineligible.
/// </summary>
[HarmonyPatch(typeof(Spiral), nameof(Spiral.CanEnchant))]
internal static class SpiralTokenEnchantPatch
{
    internal static bool AllowTokenSpiral;

    [HarmonyPrefix]
    private static bool CanEnchantPrefix(
        CardModel c,
        ref bool __result)
    {
        if (!AllowTokenSpiral)
            return true;

        if (c.Rarity != CardRarity.Token)
            return true;

        // Enchantments do not stack.
        // Any Token that already has an Enchantment is invalid.
        if (c.Enchantment != null)
        {
            __result = false;
            return false;
        }

        // Revere specifically permits otherwise-valid Token cards
        // to receive Spiral, bypassing Spiral's normal requirement
        // that the card be a Basic Strike or Defend.
        __result = true;
        return false;
    }
}