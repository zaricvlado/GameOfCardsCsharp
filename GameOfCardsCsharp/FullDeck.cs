using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Full deck with 52 cards (2-Ace in all suits)
    /// Used in games like Tablic, Poker, Blackjack, etc.
    /// </summary>
    public class FullDeck : Deck
    {
        public FullDeck()
        {
            Initialize();
        }

        protected override void Initialize()
        {
            cards.Clear();

            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    cards.Add(new Card(rank, suit));
                }
            }
        }
    }
}