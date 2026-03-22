using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Generic Monte Carlo + Minimax analyzer for imperfect information games
    /// Works with any game mode (Sans, Trump, Betl) via IPositionEvaluator
    /// </summary>
    public class MonteCarloMinimaxAnalyzer
    {
        private readonly IPositionEvaluator evaluator;
        private readonly int simulationCount;
        private readonly int minimaxDepth;

        public MonteCarloMinimaxAnalyzer(
            IPositionEvaluator evaluator,
            int simulationCount = 100,
            int minimaxDepth = 3)
        {
            this.evaluator = evaluator;
            this.simulationCount = simulationCount;
            this.minimaxDepth = minimaxDepth;
        }

        /// <summary>
        /// Finds best move using Monte Carlo simulations with Minimax evaluation
        /// </summary>
        public RankedMove GetBestMove(
            List<Card> myHand,
            List<Card> knownOpponentCards,
            List<Card> unknownCards,
            int currentLeader)
        {
            var moveScores = new ConcurrentDictionary<Card, ConcurrentBag<double>>();

            foreach (var card in myHand)
            {
                moveScores[card] = new ConcurrentBag<double>();
            }

            // Run simulations in parallel
            Parallel.For(0, simulationCount, i =>
            {
                // Generate random opponent hand from unknown cards
                var simulatedOpponentHand = GenerateOpponentHand(
                    knownOpponentCards,
                    unknownCards,
                    10 - knownOpponentCards.Count);

                // Evaluate each possible move using Minimax
                foreach (var card in myHand)
                {
                    var testMyHand = new List<Card>(myHand);
                    testMyHand.Remove(card);

                    // Run minimax from this position
                    var score = Minimax(
                        testMyHand,
                        simulatedOpponentHand,
                        currentLeader,
                        depth: 0,
                        alpha: double.MinValue,
                        beta: double.MaxValue,
                        maximizingPlayer: false); // Opponent moves next

                    moveScores[card].Add(score);
                }
            });

            // Aggregate results
            var results = moveScores.Select(kvp => new
            {
                Card = kvp.Key,
                Scores = kvp.Value.ToList(),
                AverageScore = kvp.Value.Average(),
                MedianScore = CalculateMedian(kvp.Value.ToList()),
                MinScore = kvp.Value.Min(),
                MaxScore = kvp.Value.Max(),
                StdDev = CalculateStdDev(kvp.Value.ToList())
            }).OrderByDescending(x => x.AverageScore)
              .ThenBy(x => x.StdDev)
              .ToList();

            var best = results.First();

            return new RankedMove
            {
                Card = best.Card,
                ExpectedTricks = best.AverageScore,
                Confidence = CalculateConfidence(best.StdDev),
                MinScore = best.MinScore,
                MaxScore = best.MaxScore,
                StandardDeviation = best.StdDev,
                Alternatives = results.Skip(1).Take(3).Select(r => new AlternativeMove
                {
                    Card = r.Card,
                    ExpectedTricks = r.AverageScore,
                    Confidence = CalculateConfidence(r.StdDev)
                }).ToList(),
                Reasoning = $"Monte Carlo ({simulationCount} sims, depth {minimaxDepth}): " +
                           $"Avg={best.AverageScore:F2}, StdDev={best.StdDev:F2}"
            };
        }

        private double Minimax(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader,
            int depth,
            double alpha,
            double beta,
            bool maximizingPlayer)
        {
            // Terminal condition: Reached depth limit
            if (depth >= minimaxDepth || myHand.Count == 0 || opponentHand.Count == 0)
            {
                return evaluator.EvaluatePosition(myHand, opponentHand, currentLeader);
            }

            if (maximizingPlayer)
            {
                // My turn - maximize score
                double maxEval = double.MinValue;

                foreach (var card in myHand)
                {
                    var newMyHand = new List<Card>(myHand);
                    newMyHand.Remove(card);

                    double eval = Minimax(
                        newMyHand,
                        opponentHand,
                        currentLeader,
                        depth + 1,
                        alpha,
                        beta,
                        false);

                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);

                    if (beta <= alpha)
                        break; // Beta cutoff
                }

                return maxEval;
            }
            else
            {
                // Opponent's turn - minimize my score
                double minEval = double.MaxValue;

                foreach (var card in opponentHand)
                {
                    var newOppHand = new List<Card>(opponentHand);
                    newOppHand.Remove(card);

                    double eval = Minimax(
                        myHand,
                        newOppHand,
                        currentLeader,
                        depth + 1,
                        alpha,
                        beta,
                        true);

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);

                    if (beta <= alpha)
                        break; // Alpha cutoff
                }

                return minEval;
            }
        }

        private List<Card> GenerateOpponentHand(
            List<Card> knownCards,
            List<Card> unknownCards,
            int requiredCards)
        {
            var opponentHand = new List<Card>(knownCards);
            var shuffled = unknownCards.OrderBy(_ => Random.Shared.Next()).ToList();
            opponentHand.AddRange(shuffled.Take(requiredCards));
            return opponentHand;
        }

        private double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        private double CalculateStdDev(List<double> values)
        {
            double avg = values.Average();
            double sumSquaredDiffs = values.Sum(x => Math.Pow(x - avg, 2));
            return Math.Sqrt(sumSquaredDiffs / values.Count);
        }

        private double CalculateConfidence(double stdDev)
        {
            // Lower standard deviation = higher confidence
            return Math.Max(0, 1.0 - (stdDev / 3.0));
        }
    }
}
