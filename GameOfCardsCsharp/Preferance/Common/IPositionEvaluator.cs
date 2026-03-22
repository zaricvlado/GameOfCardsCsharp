using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Interface for game-specific position evaluation
    /// Used by MonteCarloMinimaxAnalyzer to evaluate terminal positions
    /// </summary>
    public interface IPositionEvaluator
    {
        /// <summary>
        /// Evaluates a position and returns a score from the perspective of "my" player
        /// Positive = good for me, Negative = good for opponent
        /// </summary>
        /// <param name="myHand">My remaining cards</param>
        /// <param name="opponentHand">Opponent's remaining cards</param>
        /// <param name="currentLeader">Player who leads the next trick (0 = me, 1 = opponent)</param>
        /// <returns>Position score (positive = advantage for me)</returns>
        double EvaluatePosition(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader);
    }
}
