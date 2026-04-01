using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    public class Hand
    {
        private readonly List<Card> cards = new();

        public void AddCard(Card card)
        {
            cards.Add(card);
        }

        public void RemoveCard(int index)
        {
            if (index >= 0 && index < cards.Count)
            {
                cards.RemoveAt(index);
            }
        }
        public void RemoveCard(Card card)
        {
            cards.Remove(card);
        }

        public void Clear()
        {
            cards.Clear();
        }

        public IReadOnlyList<Card> GetCards() => cards.AsReadOnly();

        public int CardCount() => cards.Count;

        public void SortByRank(GameRules rules)
        {
            cards.Sort((a, b) => rules.IsLess(a, b) ? -1 : rules.IsLess(b, a) ? 1 : 0);
        }

        public void SortBySuit()
        {
            cards.Sort((a, b) => a.Suit.CompareTo(b.Suit));
        }

        /// <summary>
        /// Sorts cards by suit, then by rank in descending order (high cards first).
        /// Suits are ordered: Clubs, Diamonds, Hearts, Spades.
        /// Within each suit: Ace, King, Queen, Jack, 10, 9, 8, 7.
        /// </summary>
        public void SortBySuitThenRankDescending()
        {
            cards.Sort((a, b) =>
            {
                // First compare by suit
                if (a.Suit != b.Suit)
                    return a.Suit.CompareTo(b.Suit);
                
                // Same suit - compare by rank descending (higher rank first)
                return b.Rank.CompareTo(a.Rank);
            });
        }

        public void SortBySuitThenRank(GameRules rules)
        {
            cards.Sort((a, b) =>
            {
                if (a.Suit != b.Suit)
                    return a.Suit.CompareTo(b.Suit);
                return rules.IsLess(a, b) ? -1 : rules.IsLess(b, a) ? 1 : 0;
            });
        }

        public int CalculateTotal(GameRules rules)
        {
            return cards.Sum(card => rules.GetCardValue(card));
        }

        public int CalculateTricks(GameRules rules)
        {
            return cards.Sum(card => rules.GetTrickValue(card));
        }
    }
}