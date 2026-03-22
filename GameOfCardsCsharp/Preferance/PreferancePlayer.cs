using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance
{
    /// <summary>
    /// Represents a player in Preferance game
    /// </summary>
    public class PreferancePlayer
    {
        private readonly int playerId;
        private readonly Hand hand = new();
        private readonly List<Card> pile = new();

        public PreferancePlayer(int playerId)
        {
            this.playerId = playerId;
        }

        public int GetPlayerId() => playerId;

        public Hand GetHand() => hand;

        public void AddToPile(Card card)
        {
            pile.Add(card);
        }

        public void AddToPile(IEnumerable<Card> cards)
        {
            pile.AddRange(cards);
        }

        public IReadOnlyList<Card> GetPile() => pile.AsReadOnly();

        public int GetPileSize() => pile.Count;

        public void Reset()
        {
            hand.Clear();
            pile.Clear();
        }
    }
}
