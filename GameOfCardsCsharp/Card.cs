using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    public enum Suit
    {
        Clubs,
        Diamonds,
        Hearts,
        Spades
    }

    public enum Rank
    {
        Two = 0,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Ten,
        Jack,
        Queen,
        King,
        Ace
    }

    public class Card
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public override string ToString()
        {
            return $"{RankToString(Rank)} of {SuitToString(Suit)}";
        }

        private static string RankToString(Rank rank)
        {
            return rank switch
            {
                Rank.Two => "2",
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "Jack",
                Rank.Queen => "Queen",
                Rank.King => "King",
                Rank.Ace => "Ace",
                _ => "Unknown"
            };
        }

        private static string SuitToString(Suit suit)
        {
            return suit switch
            {
                Suit.Clubs => "Clubs",
                Suit.Diamonds => "Diamonds",
                Suit.Hearts => "Hearts",
                Suit.Spades => "Spades",
                _ => "Unknown"
            };
        }
    }
}