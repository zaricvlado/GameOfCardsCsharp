using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Analyzes suit control and stoppers for trick-taking evaluation
    /// </summary>
    public class SuitControlAnalyzer
    {
        public SuitControlReport AnalyzeSuitControl(
            List<Card> myHand,
            List<Card> opponentHand)
        {
            var report = new SuitControlReport();

            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                var mySuitCards = myHand
                    .Where(c => c.Suit == suit)
                    .OrderByDescending(c => c.Rank)
                    .ToList();

                var oppSuitCards = opponentHand
                    .Where(c => c.Suit == suit)
                    .OrderByDescending(c => c.Rank)
                    .ToList();

                var control = AnalyzeSingleSuit(mySuitCards, oppSuitCards);
                report.SuitControls[suit] = control;
            }

            return report;
        }

        private SuitControl AnalyzeSingleSuit(List<Card> myCards, List<Card> oppCards)
        {
            if (myCards.Count == 0)
                return new SuitControl 
                { 
                    MyStoppers = 0, 
                    OppStoppers = oppCards.Count, 
                    Controller = Player.Opponent,
                    MySuitLength = 0,
                    OppSuitLength = oppCards.Count
                };

            if (oppCards.Count == 0)
                return new SuitControl 
                { 
                    MyStoppers = myCards.Count, 
                    OppStoppers = 0, 
                    Controller = Player.Me,
                    MySuitLength = myCards.Count,
                    OppSuitLength = 0
                };

            int myStoppers = 0;
            int oppStoppers = 0;

            // Create sets for O(1) lookup
            var mySet = new HashSet<Card>(myCards);
            var oppSet = new HashSet<Card>(oppCards);

            var allSorted = myCards.Concat(oppCards)
                .OrderByDescending(c => c.Rank)
                .ToList();

            // Count consecutive winners from the top
            Rank expectedRank = Rank.Ace;

            foreach (var card in allSorted)
            {
                if (card.Rank != expectedRank)
                    break; // Gap in sequence, no more guaranteed stoppers

                if (mySet.Contains(card))
                    myStoppers++;
                else
                    oppStoppers++;

                expectedRank = (Rank)((int)expectedRank - 1);

                if ((int)expectedRank < (int)Rank.Seven)
                    break;
            }

            var controller = myStoppers > oppStoppers ? Player.Me :
                            oppStoppers > myStoppers ? Player.Opponent :
                            Player.Contested;

            return new SuitControl
            {
                MyStoppers = myStoppers,
                OppStoppers = oppStoppers,
                Controller = controller,
                MySuitLength = myCards.Count,
                OppSuitLength = oppCards.Count
            };
        }

        /// <summary>
        /// Estimates expected tricks based on suit control (returns relative advantage)
        /// </summary>
        public double EstimateExpectedTricks(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            var (myTricks, oppTricks) = EstimateTrickCounts(myHand, opponentHand, currentLeader);
            return myTricks - oppTricks; // Relative advantage
        }

        /// <summary>
        /// Estimates absolute trick counts for both players
        /// </summary>
        public (double myTricks, double opponentTricks) EstimateTrickCounts(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            var report = AnalyzeSuitControl(myHand, opponentHand);

            double expectedMyTricks = 0;
            double expectedOppTricks = 0;

            foreach (var suitControl in report.SuitControls.Values)
            {
                // Guaranteed stoppers
                expectedMyTricks += suitControl.MyStoppers;
                expectedOppTricks += suitControl.OppStoppers;

                // Estimate additional tricks from suit length
                int myRemainingCards = suitControl.MySuitLength - suitControl.MyStoppers;
                int oppRemainingCards = suitControl.OppSuitLength - suitControl.OppStoppers;

                if (suitControl.Controller == Player.Me)
                {
                    expectedMyTricks += myRemainingCards * 0.6; // 60% chance with control
                }
                else if (suitControl.Controller == Player.Opponent)
                {
                    expectedOppTricks += oppRemainingCards * 0.6;
                }
                else
                {
                    // Contested - split
                    expectedMyTricks += myRemainingCards * 0.4;
                    expectedOppTricks += oppRemainingCards * 0.4;
                }
            }

            return (expectedMyTricks, expectedOppTricks);
        }
    }
}
