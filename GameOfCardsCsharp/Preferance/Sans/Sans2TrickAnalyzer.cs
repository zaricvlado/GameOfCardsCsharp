using System;
using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Main API for Sans 2-player trick analysis
    /// Supports multiple strategies: Suit Control, Perfect Game Tree, Monte Carlo + Minimax
    /// </summary>
    public class Sans2TrickAnalyzer
    {
        private readonly AnalysisMode mode;
        private readonly MonteCarloMinimaxAnalyzer? mcAnalyzer;
        private readonly SansPerfectGameAnalyzer? perfectGameAnalyzer;
        private readonly SansPositionEvaluator evaluator;
        private readonly SuitControlAnalyzer suitControlAnalyzer;
        private readonly int simulationCount;

        public Sans2TrickAnalyzer(
            AnalysisMode mode = AnalysisMode.MonteCarloSimulation,
            int simulationCount = 100,
            int minimaxDepth = 5)
        {
            this.mode = mode;
            this.simulationCount = simulationCount;
            this.evaluator = new SansPositionEvaluator();
            this.suitControlAnalyzer = new SuitControlAnalyzer();
            this.perfectGameAnalyzer = new SansPerfectGameAnalyzer(mode, simulationCount);

            if (mode == AnalysisMode.MonteCarloSimulation)
            {
                this.mcAnalyzer = new MonteCarloMinimaxAnalyzer(
                    evaluator,
                    simulationCount,
                    minimaxDepth);
            }
        }

        /// <summary>
        /// Gets best move using ONLY Suit Control heuristic (fastest, for testing)
        /// Works with perfect information only
        /// </summary>
        public Common.RankedMove GetBestMoveSuitControl(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            var results = new List<(Card card, double score, double myTricks, double oppTricks)>();

            foreach (var card in myHand)
            {
                var testHand = new List<Card>(myHand);
                testHand.Remove(card);

                // Check if this card is a guaranteed winner (higher than all opponent cards in its suit)
                var opponentCardsInSuit = opponentHand.Where(c => c.Suit == card.Suit).ToList();
                bool isGuaranteedWinner = !opponentCardsInSuit.Any() || 
                                         opponentCardsInSuit.All(c => card.Rank > c.Rank);

                var (myTricks, oppTricks) = suitControlAnalyzer.EstimateTrickCounts(
                    testHand,
                    opponentHand,
                    currentLeader);

                // Add +1 if this card is guaranteed to win a trick
                if (isGuaranteedWinner)
                {
                    myTricks += 1.0;
                }

                var score = myTricks - oppTricks;
                results.Add((card, score, myTricks, oppTricks));
            }

            var best = results.OrderByDescending(r => r.score).First();

            return new Common.RankedMove
            {
                Card = best.card,
                ExpectedTricks = best.score,
                PredictedMyTricks = best.myTricks,
                PredictedOpponentTricks = best.oppTricks,
                Confidence = 0.7,
                MinScore = best.score,
                MaxScore = best.score,
                StandardDeviation = 0,
                Reasoning = "Suit Control Heuristic: Fast estimation based on stoppers"
            };
        }

        /// <summary>
        /// Gets detailed suit control analysis for a position
        /// </summary>
        public SuitControlReport GetSuitControlReport(
            List<Card> myHand,
            List<Card> opponentHand)
        {
            return suitControlAnalyzer.AnalyzeSuitControl(myHand, opponentHand);
        }

        /// <summary>
        /// Gets best move in Perfect Information mode using perfect game tree search
        /// This uses the SansPerfectGame analyzer for optimal play
        /// </summary>
        public Common.RankedMove GetBestMovePerfectInfo(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            if (mode != AnalysisMode.PerfectInformation)
                throw new InvalidOperationException(
                    "This method is only for PerfectInformation mode. Use GetBestMoveMonteCarlo() for Monte Carlo.");

            if (perfectGameAnalyzer == null)
                throw new InvalidOperationException("Perfect game analyzer not initialized");

            // Use the new perfect game analyzer
            return perfectGameAnalyzer.GetBestMovePerfectInfo(myHand, opponentHand, currentLeader);
        }

        /// <summary>
        /// Gets best move using legacy position evaluator (for comparison/fallback)
        /// </summary>
        public Common.RankedMove GetBestMovePerfectInfoLegacy(
            List<Card> myHand,
            List<Card> opponentHand,
            int currentLeader)
        {
            var results = new List<(Card card, double score, double myTricks, double oppTricks)>();

            foreach (var card in myHand)
            {
                var testHand = new List<Card>(myHand);
                testHand.Remove(card);

                var score = evaluator.EvaluatePosition(testHand, opponentHand, currentLeader);
                var (myTricks, oppTricks) = evaluator.EstimateTrickCounts(testHand, opponentHand, currentLeader);

                results.Add((card, score, myTricks, oppTricks));
            }

            var best = results.OrderByDescending(r => r.score).First();

            return new Common.RankedMove
            {
                Card = best.card,
                ExpectedTricks = best.score,
                PredictedMyTricks = best.myTricks,
                PredictedOpponentTricks = best.oppTricks,
                Confidence = 1.0,
                MinScore = best.score,
                MaxScore = best.score,
                StandardDeviation = 0,
                Reasoning = "Perfect Information (Legacy): Position evaluator heuristic"
            };
        }

        /// <summary>
        /// Gets best move in Monte Carlo mode (opponent cards unknown)
        /// Can use either minimax analyzer or perfect game analyzer
        /// </summary>
        public Common.RankedMove GetBestMoveMonteCarlo(
            List<Card> myHand,
            List<Card> cardsPlayed,
            int currentLeader,
            bool usePerfectGameAnalyzer = false)
        {
            if (mode != AnalysisMode.MonteCarloSimulation)
                throw new InvalidOperationException(
                    "This method is only for MonteCarloSimulation mode. Use GetBestMovePerfectInfo() for Perfect Information.");

            var allCards = GetFullDeck();
            var knownCards = myHand.Concat(cardsPlayed).ToList();
            var unknownCards = allCards.Except(knownCards, new CardEqualityComparer()).ToList();

            if (usePerfectGameAnalyzer && perfectGameAnalyzer != null)
            {
                // Use the new perfect game analyzer with Monte Carlo
                return perfectGameAnalyzer.GetBestMoveMonteCarlo(
                    myHand,
                    new List<Card>(), // No known opponent cards
                    unknownCards,
                    currentLeader);
            }
            else
            {
                // Use legacy minimax analyzer
                if (mcAnalyzer == null)
                    throw new InvalidOperationException("Monte Carlo analyzer not initialized");

                return mcAnalyzer.GetBestMove(
                    myHand,
                    new List<Card>(), // No known opponent cards
                    unknownCards,
                    currentLeader);
            }
        }

        /// <summary>
        /// Gets best following move when opponent has led
        /// </summary>
        public Common.RankedMove GetBestFollowingMove(
            List<Card> myHand,
            List<Card> opponentHand,
            Card leadCard,
            int currentLeader)
        {
            var sameSuit = myHand.Where(c => c.Suit == leadCard.Suit).ToList();

            if (sameSuit.Any())
            {
                // Must follow suit
                var canWin = sameSuit.Where(c => c.Rank > leadCard.Rank).ToList();

                if (canWin.Any())
                {
                    // Win with lowest card that beats lead
                    var bestCard = canWin.OrderBy(c => c.Rank).First();
                    return new Common.RankedMove
                    {
                        Card = bestCard,
                        ExpectedTricks = 1.0,
                        PredictedMyTricks = 1.0,
                        PredictedOpponentTricks = 0,
                        Confidence = 1.0,
                        Reasoning = $"Following {leadCard.Suit}: Win with lowest ({bestCard.Rank})"
                    };
                }
                else
                {
                    // Can't win - discard lowest
                    var lowestCard = sameSuit.OrderBy(c => c.Rank).First();
                    return new Common.RankedMove
                    {
                        Card = lowestCard,
                        ExpectedTricks = 0,
                        PredictedMyTricks = 0,
                        PredictedOpponentTricks = 1.0,
                        Confidence = 1.0,
                        Reasoning = $"Following {leadCard.Suit}: Can't win, discard lowest ({lowestCard.Rank})"
                    };
                }
            }
            else
            {
                // Can't follow suit - discard weakest card
                var weakestCard = myHand.OrderBy(c => c.Rank).First();
                return new Common.RankedMove
                {
                    Card = weakestCard,
                    ExpectedTricks = 0,
                    PredictedMyTricks = 0,
                    PredictedOpponentTricks = 1.0,
                    Confidence = 1.0,
                    Reasoning = $"Can't follow {leadCard.Suit}: Discard weakest card ({weakestCard})"
                };
            }
        }

        private List<Card> GetFullDeck()
        {
            var allCards = new List<Card>();

            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                for (Rank rank = Rank.Seven; rank <= Rank.Ace; rank++)
                {
                    allCards.Add(new Card(rank, suit));
                }
            }

            return allCards;
        }

        private class CardEqualityComparer : IEqualityComparer<Card>
        {
            public bool Equals(Card? x, Card? y)
            {
                if (x == null || y == null) return false;
                return x.Rank == y.Rank && x.Suit == y.Suit;
            }

            public int GetHashCode(Card obj)
            {
                return HashCode.Combine(obj.Rank, obj.Suit);
            }
        }
    }
}
