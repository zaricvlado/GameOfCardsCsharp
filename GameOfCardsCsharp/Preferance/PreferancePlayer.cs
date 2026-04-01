using System;
using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Bidding;

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
        private PlayerBidContext? _bidContext;

        public PreferancePlayer(int playerId)
        {
            this.playerId = playerId;
        }

        public int GetPlayerId() => playerId;

        public Hand GetHand() => hand;

        public PlayerRole Role { get; set; } = PlayerRole.None;

        public PlayerState State { get; set; } = PlayerState.None;

        /// <summary>
        /// Gets the current bid context (null if not in bidding phase)
        /// </summary>
        public PlayerBidContext? BidContext => _bidContext;

        /// <summary>
        /// Links this player to a bid context during bidding phase.
        /// Called internally by BiddingEngine.
        /// </summary>
        internal void SetBidContext(PlayerBidContext? context)
        {
            _bidContext = context;
        }

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
            Role = PlayerRole.None;
            State = PlayerState.None;
            _bidContext = null;
        }
    }
}
