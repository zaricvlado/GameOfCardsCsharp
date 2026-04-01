using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// State machine for Trump games
    /// </summary>
    internal class TrumpGameStateMachine : GamePlayStateMachine
    {
        private readonly Suit _trumpSuit;

        public TrumpGameStateMachine(
            RoleAssignmentResult roles,
            Dictionary<int, PreferancePlayer> players)
            : base(roles, players)
        {
            _trumpSuit = roles.TrumpSuit switch
            {
                TrumpSuit.Spades => Suit.Spades,
                TrumpSuit.Diamonds => Suit.Diamonds,
                TrumpSuit.Hearts => Suit.Hearts,
                TrumpSuit.Clubs => Suit.Clubs,
                _ => throw new ArgumentException("Trump suit must be specified for Trump games")
            };
        }

        public override List<Card> GetLegalMoves(int playerId)
        {
            var hand = _players[playerId].GetHand().GetCards();
            var leadCard = GetLeadCard();

            // If leading, can play any card
            if (leadCard == null)
                return new List<Card>(hand);

            // Must follow suit if possible
            var sameSuit = hand.Where(c => c.Suit == leadCard.Suit).ToList();
            if (sameSuit.Any())
                return sameSuit;

            // If can't follow suit but have trump, must play trump
            var trumpCards = hand.Where(c => c.Suit == _trumpSuit).ToList();
            if (trumpCards.Any())
                return trumpCards;

            // If no same suit and no trump, can play any card
            return new List<Card>(hand);
        }

        protected override int DetermineWinner(TrickState trick)
        {
            var leadCard = trick.CardsPlayed.Values.First();
            var leadSuit = leadCard.Suit;

            Card? winningCard = null;
            int winnerId = -1;
            bool winningIsTrump = false;

            foreach (var kvp in trick.CardsPlayed)
            {
                var card = kvp.Value;
                bool isTrump = card.Suit == _trumpSuit;

                if (winningCard == null)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                    winningIsTrump = isTrump;
                    continue;
                }

                // Trump beats non-trump
                if (isTrump && !winningIsTrump)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                    winningIsTrump = true;
                }
                // Higher trump beats lower trump
                else if (isTrump && winningIsTrump && card.Rank > winningCard.Rank)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                }
                // Same suit (non-trump) - higher rank wins
                else if (!isTrump && !winningIsTrump && card.Suit == leadSuit && card.Rank > winningCard.Rank)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                }
            }

            return winnerId;
        }

        protected override bool EvaluateDeclarerSuccess(int declarerTricks)
        {
            // In Trump, declarer needs more than half (6+ out of 10)
            var totalTricks = _tricks.Count;
            return declarerTricks > (totalTricks / 2);
        }

        // Trump uses default AwardTrickCards: cards go to trick winner
    }
}
