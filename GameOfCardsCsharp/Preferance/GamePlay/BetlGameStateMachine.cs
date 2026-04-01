using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// State machine for Betl (no-trump, lose all tricks) games
    /// </summary>
    internal class BetlGameStateMachine : GamePlayStateMachine
    {
        public BetlGameStateMachine(
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

        /// <summary>
        /// Betl special rule: ALL cards go to Declarer's pile (not to trick winner)
        /// This prevents opponents from reviewing past plays.
        /// Note: Pile ownership != trick winner (tracked separately in _actualTrickCounts)
        /// </summary>
        protected override void AwardTrickCards(TrickState trick, int winnerId)
        {
            // In Betl, ALL cards go to Declarer's pile
            var declarerId = _roles.DeclarerId;
            
            foreach (var card in trick.CardsPlayed.Values)
            {
                _players[declarerId].AddToPile(card);
            }
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

                // Only same suit can win (no trump)
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
            // In Betl, declarer must win ZERO tricks
            // Success based on trick count, NOT pile size
            return declarerTricks == 0;
        }
    }
}
