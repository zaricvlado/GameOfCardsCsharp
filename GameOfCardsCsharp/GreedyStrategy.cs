using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Greedy strategy - always picks the best evaluated move
    /// </summary>
    public class GreedyStrategy : PlayerStrategyBase
    {
        public GreedyStrategy(GameRules rules) : base(rules)
        {
        }

        public override PlayerMove DecideMove(TablicGameState state, List<PossibleMove> possibleMoves)
        {
            if (possibleMoves.Count == 0)
            {
                return new PlayerMove { HandIndex = 0, AttemptPickup = false };
            }

            int bestScore = int.MinValue;
            var bestMove = new PlayerMove { HandIndex = 0, AttemptPickup = false };

            foreach (var move in possibleMoves)
            {
                int score = EvaluateMove(move, state);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove.HandIndex = move.HandIndex;
                    bestMove.AttemptPickup = move.CanPickup && move.PossibleCombinations.Count > 0;

                    if (bestMove.AttemptPickup && move.PossibleCombinations.Count > 0)
                    {
                        // Select best combination based on evaluation
                        int bestComboScore = int.MinValue;
                        foreach (var combo in move.PossibleCombinations)
                        {
                            int trickValue = combo.Sum(idx => rules.GetTrickValue(state.GetTalon()[idx]));
                            int cardCount = combo.Count;
                            int comboScore = (trickValue * 10) + cardCount;

                            if (comboScore > bestComboScore)
                            {
                                bestComboScore = comboScore;
                                bestMove.TalonIndices = combo;
                            }
                        }
                    }
                }
            }

            return bestMove;
        }

        public override string GetStrategyName() => "Greedy";
    }
}