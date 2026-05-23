using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Tablic
{
    /// <summary>
    /// Base class for player strategies with default evaluation logic
    /// </summary>
    public abstract class PlayerStrategyBase : IPlayerStrategy
    {
        protected readonly GameRules rules;

        protected PlayerStrategyBase(GameRules rules)
        {
            this.rules = rules;
        }

        public abstract PlayerMove DecideMove(TablicGameState state, List<PossibleMove> possibleMoves);
        
        public abstract string GetStrategyName();

        /// <summary>
        /// Default evaluation logic that all strategies can use
        /// Priority: Pickup > Trick Value > Card Count
        /// </summary>
        public virtual int EvaluateMove(PossibleMove move, TablicGameState state)
        {
            int score = 0;

            if (move.CanPickup && move.PossibleCombinations.Count > 0)
            {
                // Base pickup bonus
                score += 1000;

                // Find best combination by trick value, then card count
                int bestComboScore = 0;
                foreach (var combo in move.PossibleCombinations)
                {
                    int trickValue = combo.Sum(idx => rules.GetTrickValue(state.GetTalon()[idx]));
                    int cardCount = combo.Count;
                    
                    // Trick value is worth 10 points each, card count is worth 1 point each
                    int comboScore = trickValue * 10 + cardCount;
                    
                    bestComboScore = Math.Max(bestComboScore, comboScore);
                }
                
                score += bestComboScore;
            }
            else
            {
                // Not a pickup - playing card to talon
                // Evaluate based on card's trick value (lower is better to discard)
                int trickValue = rules.GetTrickValue(move.Card);
                
                // Penalize discarding valuable trick cards
                score -= trickValue * 5;
            }

            return score;
        }
    }
}
