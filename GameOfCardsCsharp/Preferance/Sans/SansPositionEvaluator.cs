using System.Collections.Generic;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Evaluates positions for Sans game (maximize tricks, no trump)
    /// </summary>
    public class SansPositionEvaluator : IPositionEvaluator
    {
        private readonly SuitControlAnalyzer suitControlAnalyzer;

        public SansPositionEvaluator()
        {
            suitControlAnalyzer = new SuitControlAnalyzer();
        }

        public double EvaluatePosition(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            // Use Suit Control to estimate expected tricks (relative advantage)
            return suitControlAnalyzer.EstimateExpectedTricks(
                myHand,
                opponentHand,
                currentLeader);
        }

        /// <summary>
        /// Estimates absolute trick counts for both players
        /// </summary>
        public (double myTricks, double opponentTricks) EstimateTrickCounts(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            return suitControlAnalyzer.EstimateTrickCounts(myHand, opponentHand, currentLeader);
        }
    }
}
