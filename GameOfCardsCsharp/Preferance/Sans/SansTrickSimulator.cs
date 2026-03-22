using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Simulates individual tricks in Sans (no-trump) game
    /// </summary>
    public class SansTrickSimulator
    {
        /// <summary>
        /// Determines the winner of a trick in Sans (no trump)
        /// </summary>
        public int DetermineTrickWinner(Card leadCard, Card followCard, int leaderPlayerId)
        {
            // In Sans (no trump), must follow suit
            if (followCard.Suit != leadCard.Suit)
                return leaderPlayerId; // Leader wins (follower couldn't follow)

            // Same suit - highest rank wins
            return leadCard.Rank > followCard.Rank ? leaderPlayerId : 1 - leaderPlayerId;
        }

        /// <summary>
        /// Chooses best follow card given the lead
        /// </summary>
        public Card ChooseFollowCard(List<Card> hand, Card leadCard)
        {
            var sameSuit = hand.Where(c => c.Suit == leadCard.Suit).ToList();

            if (sameSuit.Any())
            {
                // Must follow suit
                var canWin = sameSuit.Where(c => c.Rank > leadCard.Rank).ToList();
                
                if (canWin.Any())
                {
                    // Win with lowest card that beats lead
                    return canWin.OrderBy(c => c.Rank).First();
                }
                else
                {
                    // Can't win - discard lowest
                    return sameSuit.OrderBy(c => c.Rank).First();
                }
            }
            else
            {
                // Can't follow suit - discard weakest card
                return hand.OrderBy(c => c.Rank).First();
            }
        }

        /// <summary>
        /// Chooses strategic lead card
        /// </summary>
        public Card ChooseLeadCard(List<Card> hand, List<Card> opponentHand)
        {
            // Strategy: Lead from longest suit with high cards
            var suitGroups = hand.GroupBy(c => c.Suit)
                .Select(g => new
                {
                    Suit = g.Key,
                    Cards = g.OrderByDescending(c => c.Rank).ToList(),
                    Length = g.Count(),
                    HighestRank = g.Max(c => c.Rank),
                    HasAce = g.Any(c => c.Rank == Rank.Ace)
                })
                .OrderByDescending(g => g.HasAce ? 1 : 0)
                .ThenByDescending(g => g.Length)
                .ThenByDescending(g => g.HighestRank)
                .ToList();

            if (suitGroups.Any())
            {
                var bestSuit = suitGroups.First();
                // Lead highest card in best suit
                return bestSuit.Cards.First();
            }

            // Fallback
            return hand.OrderByDescending(c => c.Rank).First();
        }
    }
}
