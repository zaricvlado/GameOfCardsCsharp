using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// State machine for Sans (no-trump, win all/most tricks) games
    /// </summary>
    internal class SansGameStateMachine : GamePlayStateMachine
    {
        public SansGameStateMachine(
            RoleAssignmentResult roles,
            Dictionary<int, PreferancePlayer> players)
            : base(roles, players)
        {
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

            // If can't follow suit, can play any card
            return new List<Card>(hand);
        }

        protected override int DetermineWinner(TrickState trick)
        {
            var leadCard = trick.CardsPlayed.Values.First();
            var leadSuit = leadCard.Suit;

            Card? winningCard = null;
            int winnerId = -1;

            foreach (var kvp in trick.CardsPlayed)
            {
                var card = kvp.Value;

                if (winningCard == null)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                    continue;
                }

                // Only same suit can win (no trump in Sans)
                if (card.Suit == leadSuit && card.Rank > winningCard.Rank)
                {
                    winningCard = card;
                    winnerId = kvp.Key;
                }
            }

            return winnerId;
        }

        protected override bool EvaluateDeclarerSuccess(int declarerTricks)
        {
            // In Sans, declarer needs more than half (6+ out of 10)
            var totalTricks = _tricks.Count;
            return declarerTricks > (totalTricks / 2);
        }

        // Sans uses default AwardTrickCards: cards go to trick winner
    }
}
