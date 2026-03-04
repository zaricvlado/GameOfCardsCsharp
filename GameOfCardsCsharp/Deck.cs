using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    public abstract class Deck
    {
        protected List<Card> cards = new();

        protected Deck()
        {
        }

        public void Shuffle()
        {
            var rng = new Random();
            int n = cards.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (cards[k], cards[n]) = (cards[n], cards[k]);
            }
        }

        public Card DrawCard()
        {
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("Cannot draw from empty deck");
            }

            var card = cards[^1];
            cards.RemoveAt(cards.Count - 1);
            return card;
        }

        public bool IsEmpty() => cards.Count == 0;

        public int CardsRemaining() => cards.Count;

        public void Reset()
        {
            Initialize();
        }

        protected abstract void Initialize();
    }
}