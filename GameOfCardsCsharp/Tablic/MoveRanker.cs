using System;
using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Tablic
{
    /// <summary>
    /// Ranks and selects best moves using a given strategy
    /// </summary>
    public class MoveRanker
    {
        private readonly IPlayerStrategy strategy;

        public MoveRanker(IPlayerStrategy strategy)
        {
            this.strategy = strategy;
        }

        /// <summary>
        /// Gets the top N best moves according to the strategy
        /// </summary>
        public List<TablicRankedMove> GetBestMoves(TablicGameState state, List<PossibleMove> possibleMoves, int count)
        {
            if (possibleMoves.Count == 0)
            {
                return new List<TablicRankedMove>();
            }

            // Score all moves using strategy's EvaluateMove
            var scoredMoves = possibleMoves
                .Select(move => (move, score: strategy.EvaluateMove(move, state)))
                .OrderByDescending(x => x.score)
                .Take(count)
                .Select((x, index) => new TablicRankedMove
                {
                    Move = x.move,
                    Score = x.score,
                    Rank = index + 1
                })
                .ToList();

            return scoredMoves;
        }

        /// <summary>
        /// Get all moves ranked by score
        /// </summary>
        public List<TablicRankedMove> GetAllMovesRanked(TablicGameState state, List<PossibleMove> possibleMoves)
        {
            return GetBestMoves(state, possibleMoves, possibleMoves.Count);
        }
    }

    /// <summary>
    /// A Tablic move with its evaluated score and rank
    /// </summary>
    public class TablicRankedMove
    {
        public PossibleMove Move { get; set; } = new();
        public int Score { get; set; }
        public int Rank { get; set; }
    }
}
