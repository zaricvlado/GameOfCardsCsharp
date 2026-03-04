using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Short deck with 32 cards (7-Ace in all suits)
    /// Used in games like Preferans, Belot, Euchre, etc.
    /// </summary>
    public class ShortDeck : Deck
    {
        public ShortDeck()
        {
            Initialize();
        }

        protected override void Initialize()
        {
            cards.Clear();

            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                for (Rank rank = Rank.Seven; rank <= Rank.Ace; rank++)
                {
                    cards.Add(new Card(rank, suit));
                }
            }
        }
    }
}