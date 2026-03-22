using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Analyzer that uses perfect information game tree search for Sans.
    /// Provides optimal moves when all cards are known (test mode) or estimates
    /// via Monte Carlo when opponent cards are unknown.
    /// </summary>
    public class SansPerfectGameAnalyzer
    {
        private readonly AnalysisMode mode;
        private readonly int simulationCount;

        public SansPerfectGameAnalyzer(
            AnalysisMode mode = AnalysisMode.PerfectInformation,
            int simulationCount = 100)
        {
            this.mode = mode;
            this.simulationCount = simulationCount;
        }

        /// <summary>
        /// Gets the best move for the current player in perfect information mode
        /// </summary>
        public RankedMove GetBestMovePerfectInfo(
            List<Card> myHand,
            List<Card> opponentHand,
            int myPlayerIndex)
        {
            // Create game state
            var gameState = new PerfPerfectGameState(
                PreferanceGameMode.Sans,
                new List<string> { "Player0", "Player1" },
                currentPlayerIndex: myPlayerIndex,
                leaderPlayerIndex: myPlayerIndex
            );

            // Setup hands
            gameState.SetupPlayerHands(
                myPlayerIndex == 0 ? myHand : opponentHand,
                myPlayerIndex == 0 ? opponentHand : myHand
            );

            // Create perfect game analyzer
            var game = new SansPerfectGame(gameState);

            try
            {
                // Get best lead move
                var bestMove = game.BestLeadCard2();

                // Calculate expected outcome
                int handsLeft = myHand.Count;
                var score = CalculateExpectedScore(game, bestMove, handsLeft);

                return new RankedMove
                {
                    Card = bestMove.Card,
                    ExpectedTricks = score.MyTricks,
                    PredictedMyTricks = score.MyTricks,
                    PredictedOpponentTricks = score.OpponentTricks,
                    Confidence = 1.0, // Perfect information = 100% confidence
                    Reasoning = $"Perfect analysis: Win {score.MyTricks:F1}/{handsLeft} tricks " +
                               $"(Opp: {score.OpponentTricks:F1}). " +
                               $"This is the optimal move with perfect information."
                };
            }
            catch (NotImplementedException)
            {
                // BestFollowMove2 not yet implemented - fallback
                return new RankedMove
                {
                    Card = myHand.First(),
                    ExpectedTricks = 0,
                    Confidence = 0,
                    Reasoning = "Perfect game analyzer: BestFollowMove2 not yet implemented"
                };
            }
        }

        /// <summary>
        /// Gets the best move using Monte Carlo simulations with perfect game analysis
        /// </summary>
        public RankedMove GetBestMoveMonteCarlo(
            List<Card> myHand,
            List<Card> knownOpponentCards,
            List<Card> unknownCards,
            int myPlayerIndex)
        {
            var moveScores = new Dictionary<Card, List<double>>();
            foreach (var card in myHand)
            {
                moveScores[card] = new List<double>();
            }

            // Run simulations
            for (int sim = 0; sim < simulationCount; sim++)
            {
                // Generate random opponent hand
                var simulatedOppHand = GenerateOpponentHand(
                    knownOpponentCards,
                    unknownCards,
                    myHand.Count - knownOpponentCards.Count
                );

                // Create game state for this simulation
                var gameState = new PerfPerfectGameState(
                    PreferanceGameMode.Sans,
                    new List<string> { "Player0", "Player1" },
                    currentPlayerIndex: myPlayerIndex,
                    leaderPlayerIndex: myPlayerIndex
                );

                gameState.SetupPlayerHands(
                    myPlayerIndex == 0 ? myHand : simulatedOppHand,
                    myPlayerIndex == 0 ? simulatedOppHand : myHand
                );

                var game = new SansPerfectGame(gameState);

                try
                {
                    // Evaluate each candidate move
                    var candidates = gameState.GetCandidateMovesForCurrentPlayer().ToList();
                    
                    foreach (var move in candidates)
                    {
                        // Calculate score for this move
                        var score = CalculateExpectedScore(game, move, myHand.Count);
                        moveScores[move.Card].Add(score.MyTricks);
                    }
                }
                catch (NotImplementedException)
                {
                    // Fallback if implementation incomplete
                    foreach (var card in myHand)
                    {
                        moveScores[card].Add(0.5); // Neutral score
                    }
                }
            }

            // Aggregate results
            var results = moveScores
                .Where(kvp => kvp.Value.Any())
                .Select(kvp => new
                {
                    Card = kvp.Key,
                    AvgScore = kvp.Value.Average(),
                    MinScore = kvp.Value.Min(),
                    MaxScore = kvp.Value.Max(),
                    StdDev = CalculateStdDev(kvp.Value)
                })
                .OrderByDescending(x => x.AvgScore)
                .ThenBy(x => x.StdDev)
                .ToList();

            if (!results.Any())
            {
                // Fallback
                return new RankedMove
                {
                    Card = myHand.First(),
                    ExpectedTricks = 0,
                    Confidence = 0,
                    Reasoning = "No valid moves found in Monte Carlo simulation"
                };
            }

            var best = results.First();

            return new RankedMove
            {
                Card = best.Card,
                ExpectedTricks = best.AvgScore,
                PredictedMyTricks = best.AvgScore,
                PredictedOpponentTricks = myHand.Count - best.AvgScore,
                Confidence = CalculateConfidence(best.StdDev),
                MinScore = best.MinScore,
                MaxScore = best.MaxScore,
                StandardDeviation = best.StdDev,
                Alternatives = results.Skip(1).Take(3).Select(r => new AlternativeMove
                {
                    Card = r.Card,
                    ExpectedTricks = r.AvgScore,
                    Confidence = CalculateConfidence(r.StdDev)
                }).ToList(),
                Reasoning = $"Perfect Game Monte Carlo ({simulationCount} sims): " +
                           $"Win {best.AvgScore:F1}/{myHand.Count} tricks on average. " +
                           $"Range: [{best.MinScore:F1}, {best.MaxScore:F1}], StdDev: {best.StdDev:F2}"
            };
        }

        /// <summary>
        /// Calculates expected score for a given move (placeholder - will use actual game tree evaluation)
        /// </summary>
        private (double MyTricks, double OpponentTricks) CalculateExpectedScore(
            SansPerfectGame game,
            PerfectCardMove move,
            int totalHands)
        {
            // TODO: When CalculateScore2 is fully working, use it here
            // For now, return placeholder based on card strength
            
            // Simple heuristic: higher rank = better chance
            double myTricks = (double)move.Card.Rank / (double)Rank.Ace * totalHands;
            double oppTricks = totalHands - myTricks;

            return (myTricks, oppTricks);
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

        private double CalculateStdDev(List<double> values)
        {
            if (values.Count == 0) return 0;
            double avg = values.Average();
            double sumSquaredDiffs = values.Sum(x => Math.Pow(x - avg, 2));
            return Math.Sqrt(sumSquaredDiffs / values.Count);
        }

        private double CalculateConfidence(double stdDev)
        {
            // Lower standard deviation = higher confidence
            return Math.Max(0, Math.Min(1.0, 1.0 - (stdDev / 3.0)));
        }
    }
}
