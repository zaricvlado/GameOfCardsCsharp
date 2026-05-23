using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Tablic
{
    /// <summary>
    /// Random strategy - picks moves randomly, but can still evaluate them
    /// </summary>
    public class RandomStrategy : PlayerStrategyBase
    {
        private readonly Random random = new();

        public RandomStrategy(GameRules rules) : base(rules)
        {
        }

        public override PlayerMove DecideMove(TablicGameState state, List<PossibleMove> possibleMoves)
        {
            if (possibleMoves.Count == 0)
            {
                return new PlayerMove { HandIndex = 0, AttemptPickup = false };
            }

            // Random selection - ignore evaluation
            int index = random.Next(possibleMoves.Count);
            var move = possibleMoves[index];

            var result = new PlayerMove
            {
                HandIndex = move.HandIndex,
                AttemptPickup = move.CanPickup && move.PossibleCombinations.Count > 0
            };

            if (result.AttemptPickup && move.PossibleCombinations.Count > 0)
            {
                int comboIndex = random.Next(move.PossibleCombinations.Count);
                result.TalonIndices = move.PossibleCombinations[comboIndex];
            }

            return result;
        }

        // Uses default EvaluateMove from base class
        // This allows MoveRanker to still show meaningful scores for random player's moves

        public override string GetStrategyName() => "Random";
    }
}