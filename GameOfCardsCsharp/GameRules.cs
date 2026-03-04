using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Unified game rules for ranking, card values, and trick scoring
    /// </summary>
    public abstract class GameRules
    {
        public abstract int GetRankValue(Rank rank);

        public virtual bool IsLess(Card a, Card b)
        {
            return GetRankValue(a.Rank) < GetRankValue(b.Rank);
        }

        public abstract int GetCardValue(Card card);

        public abstract int GetTrickValue(Card card);
    }

    /// <summary>
    /// Standard ranking: Ace high, no tricks
    /// </summary>
    public class StandardRules : GameRules
    {
        public override int GetRankValue(Rank rank)
        {
            return rank switch
            {
                Rank.Two => 2,
                Rank.Three => 3,
                Rank.Four => 4,
                Rank.Five => 5,
                Rank.Six => 6,
                Rank.Seven => 7,
                Rank.Eight => 8,
                Rank.Nine => 9,
                Rank.Ten => 10,
                Rank.Jack => 11,
                Rank.Queen => 12,
                Rank.King => 13,
                Rank.Ace => 14,
                _ => 0
            };
        }

        public override int GetCardValue(Card card) => GetRankValue(card.Rank);

        public override int GetTrickValue(Card card) => 0;
    }

    /// <summary>
    /// Ace low ranking: Ace = 1, no tricks
    /// </summary>
    public class AceLowRules : GameRules
    {
        public override int GetRankValue(Rank rank)
        {
            return rank switch
            {
                Rank.Ace => 1,
                Rank.Two => 2,
                Rank.Three => 3,
                Rank.Four => 4,
                Rank.Five => 5,
                Rank.Six => 6,
                Rank.Seven => 7,
                Rank.Eight => 8,
                Rank.Nine => 9,
                Rank.Ten => 10,
                Rank.Jack => 11,
                Rank.Queen => 12,
                Rank.King => 13,
                _ => 0
            };
        }

        public override int GetCardValue(Card card) => GetRankValue(card.Rank);

        public override int GetTrickValue(Card card) => 0;
    }

    /// <summary>
    /// Blackjack: Face cards = 10, Ace = 11, no tricks
    /// </summary>
    public class BlackjackRules : GameRules
    {
        public override int GetRankValue(Rank rank) => (int)rank;

        public override int GetCardValue(Card card)
        {
            return card.Rank switch
            {
                Rank.Two => 2,
                Rank.Three => 3,
                Rank.Four => 4,
                Rank.Five => 5,
                Rank.Six => 6,
                Rank.Seven => 7,
                Rank.Eight => 8,
                Rank.Nine => 9,
                Rank.Ten or Rank.Jack or Rank.Queen or Rank.King => 10,
                Rank.Ace => 11,
                _ => 0
            };
        }

        public override int GetTrickValue(Card card) => 0;
    }

    /// <summary>
    /// Tablic: Standard ranking + trick values (2♣=1, 10♦=2, 10/J/Q/K/A=1)
    /// </summary>
    public class TablicRules : GameRules
    {
        public override int GetRankValue(Rank rank)
        {
            return rank switch
            {
                Rank.Two => 2,
                Rank.Three => 3,
                Rank.Four => 4,
                Rank.Five => 5,
                Rank.Six => 6,
                Rank.Seven => 7,
                Rank.Eight => 8,
                Rank.Nine => 9,
                Rank.Ten => 10,
                Rank.Ace => 11,
                Rank.Jack => 12,
                Rank.Queen => 13,
                Rank.King => 14,
                _ => 0
            };
        }

        public override int GetCardValue(Card card) => GetRankValue(card.Rank);

        public override int GetTrickValue(Card card)
        {
            // 2 of Clubs = 1 trick
            if (card.Rank == Rank.Two && card.Suit == Suit.Clubs)
                return 1;

            // 10 of Diamonds = 2 tricks
            if (card.Rank == Rank.Ten && card.Suit == Suit.Diamonds)
                return 2;

            // All 10s, Jacks, Queens, Kings, Aces = 1 trick
            if (card.Rank is Rank.Ten or Rank.Jack or Rank.Queen or Rank.King or Rank.Ace)
                return 1;

            return 0;
        }
    }

    /// <summary>
    /// Preferans: Uses short deck, standard ranking, no tricks
    /// </summary>
    public class PreferansRules : GameRules
    {
        public override int GetRankValue(Rank rank)
        {
            return rank switch
            {
                Rank.Two => 2,
                Rank.Three => 3,
                Rank.Four => 4,
                Rank.Five => 5,
                Rank.Six => 6,
                Rank.Seven => 7,
                Rank.Eight => 8,
                Rank.Nine => 9,
                Rank.Ten => 10,
                Rank.Jack => 11,
                Rank.Queen => 12,
                Rank.King => 13,
                Rank.Ace => 14,
                _ => 0
            };
        }

        public override int GetCardValue(Card card) => GetRankValue(card.Rank);

        public override int GetTrickValue(Card card) => 0;
    }

    /// <summary>
    /// Custom rules with user-defined rank and trick values
    /// </summary>
    public class CustomRules : GameRules
    {
        private readonly int[] rankValues = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        private readonly int[,] trickValues = new int[4, 13];

        public void SetRankValue(Rank rank, int value)
        {
            int index = (int)rank;
            if (index >= 0 && index < 13)
            {
                rankValues[index] = value;
            }
        }

        public void SetTrickValue(Card card, int value)
        {
            int suitIndex = (int)card.Suit;
            int rankIndex = (int)card.Rank;

            if (suitIndex >= 0 && suitIndex < 4 && rankIndex >= 0 && rankIndex < 13)
            {
                trickValues[suitIndex, rankIndex] = value;
            }
        }

        public override int GetRankValue(Rank rank)
        {
            int index = (int)rank;
            return index >= 0 && index < 13 ? rankValues[index] : 0;
        }

        public override int GetCardValue(Card card) => GetRankValue(card.Rank);

        public override int GetTrickValue(Card card)
        {
            int suitIndex = (int)card.Suit;
            int rankIndex = (int)card.Rank;

            if (suitIndex >= 0 && suitIndex < 4 && rankIndex >= 0 && rankIndex < 13)
            {
                return trickValues[suitIndex, rankIndex];
            }
            return 0;
        }
    }
}