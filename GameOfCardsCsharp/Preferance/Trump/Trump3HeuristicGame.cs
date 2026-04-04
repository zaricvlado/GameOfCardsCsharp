using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Trump
{
    /// <summary>
    /// Heuristic-based strategy for 3-player Trump games.
    /// Uses suit analysis and simple decision rules to estimate Score3.
    /// </summary>
    public class Trump3HeuristicGame
    {
        private readonly PerfPerfectGameState _state;
        private readonly Suit _trumpSuit;
        private readonly int _declarerIndex;
        private readonly int[] _defenderIndices;

        public PerfPerfectGameState State => _state;

        public Trump3HeuristicGame(PerfPerfectGameState state, int declarerIndex)
        {
            if (state.Players.Count != 3)
            {
                throw new ArgumentException("Trump3HeuristicGame only supports 3 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Trump)
            {
                throw new ArgumentException("Trump3HeuristicGame requires Trump game mode", nameof(state));
            }

            if (state.TrumpSuit == TrumpSuit.None)
            {
                throw new ArgumentException("Trump suit must be specified for Trump games", nameof(state));
            }

            _state = state;
            _trumpSuit = ConvertTrumpSuitToSuit(state.TrumpSuit);
            _declarerIndex = declarerIndex;

            // Calculate defender indices
            _defenderIndices = new int[2];
            int defenderIdx = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i != declarerIndex)
                {
                    _defenderIndices[defenderIdx++] = i;
                }
            }
        }

        /// <summary>
        /// Converts TrumpSuit enum to Suit enum
        /// </summary>
        private static Suit ConvertTrumpSuitToSuit(TrumpSuit trumpSuit)
        {
            return trumpSuit switch
            {
                TrumpSuit.Spades => Suit.Spades,
                TrumpSuit.Diamonds => Suit.Diamonds,
                TrumpSuit.Hearts => Suit.Hearts,
                TrumpSuit.Clubs => Suit.Clubs,
                _ => throw new ArgumentException($"Invalid trump suit: {trumpSuit}")
            };
        }

        /// <summary>
        /// Returns the best lead card using heuristic analysis
        /// </summary>
        public PerfectCardMove BestLeadCard()
        {
            int currentPlayer = _state.CurrentPlayerIndex;

            // Analyze all suits for the current player
            var suitAnalyses = AnalyzeAllSuits(currentPlayer);

            // Select best suit to lead from
            var leadStrategy = SelectLeadStrategy(suitAnalyses, currentPlayer);

            return leadStrategy.LeadCard;
        }

        /// <summary>
        /// Returns the best follow card using heuristic rules
        /// </summary>
        public PerfectCardMove BestFollowCard(PerfectCardMove leadMove, PerfectCardMove? firstFollowMove = null)
        {
            int currentPlayer = _state.CurrentPlayerIndex;
            int leadPlayer = leadMove.PlayerIndex;
            bool isSecondPlayer = (firstFollowMove == null);

            // Analyze all suits for context
            var suitAnalyses = AnalyzeAllSuits(currentPlayer);

            if (isSecondPlayer)
            {
                return SelectSecondPlayerFollow(leadMove, leadPlayer, currentPlayer, suitAnalyses);
            }
            else
            {
                return SelectThirdPlayerFollow(leadMove, firstFollowMove, leadPlayer, currentPlayer, suitAnalyses);
            }
        }

        /// <summary>
        /// Estimates the final Score3 by simulating the game using heuristics
        /// </summary>
        public Score3 EstimateScore()
        {
            // Create a copy of the state to simulate
            var simulationState = CloneState();
            var simulator = new Trump3HeuristicGame(simulationState, _declarerIndex);

            int[] trickCounts = new int[3];
            int tricksRemaining = CountRemainingTricks();

            for (int trickNum = 0; trickNum < tricksRemaining; trickNum++)
            {
                // Get lead move
                var leadMove = simulator.BestLeadCard();
                simulator.MarkAsPlayed(leadMove);

                // Get next player
                int player2Index = GetNextPlayer(simulationState.CurrentPlayerIndex);
                simulationState.CurrentPlayerIndex = player2Index;

                // Get first follow move
                var follow1Move = simulator.BestFollowCard(leadMove, null);
                simulator.MarkAsPlayed(follow1Move);

                // Get third player
                int player3Index = GetNextPlayer(simulationState.CurrentPlayerIndex);
                simulationState.CurrentPlayerIndex = player3Index;

                // Get second follow move
                var follow2Move = simulator.BestFollowCard(leadMove, follow1Move);
                simulator.MarkAsPlayed(follow2Move);

                // Determine winner
                int winnerId = DetermineWinner(leadMove, follow1Move, follow2Move);
                trickCounts[winnerId]++;

                // Winner leads next trick
                simulationState.CurrentPlayerIndex = winnerId;
            }

            // Build Score3
            int declarerTricks = trickCounts[_declarerIndex];
            int defendersTricks = trickCounts[_defenderIndices[0]] + trickCounts[_defenderIndices[1]];

            return new Score3(declarerTricks, defendersTricks, trickCounts);
        }

        // ==================== SUIT ANALYSIS ====================

        /// <summary>
        /// Analyzes all four suits for a player (or coalition)
        /// </summary>
        private List<SuitAnalysisResult> AnalyzeAllSuits(int playerIndex)
        {
            bool isDefender = (playerIndex != _declarerIndex);
            var results = new List<SuitAnalysisResult>();

            for (int suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                var suit = (Suit)suitIndex;
                var analysis = AnalyzeSuit(suit, playerIndex, isDefender);
                results.Add(analysis);
            }

            return results;
        }

        /// <summary>
        /// Analyzes a single suit for control and strength
        /// </summary>
        private SuitAnalysisResult AnalyzeSuit(Suit suit, int playerIndex, bool isDefender)
        {
            var suitMoves = _state.Moves[(int)suit];

            // Find highest available card in suit
            PerfectCardMove? highestCard = null;
            int highestOwner = -1;

            foreach (var move in suitMoves)
            {
                if (move.Available)
                {
                    highestCard = move;
                    highestOwner = move.PlayerIndex;
                    break; // Cards are sorted high to low
                }
            }

            bool hasStrongestCard = false;
            if (highestCard != null)
            {
                if (isDefender)
                {
                    // Defenders: check if either defender has strongest card
                    hasStrongestCard = _defenderIndices.Contains(highestOwner);
                }
                else
                {
                    // Declarer: check if declarer has strongest card
                    hasStrongestCard = (highestOwner == playerIndex);
                }
            }

            // Count cards and estimate wins
            int cardCount = 0;
            int estimatedWins = 0;

            if (isDefender)
            {
                // For defenders, count both defenders' cards combined
                var defenderCards = suitMoves
                    .Where(m => m.Available && _defenderIndices.Contains(m.PlayerIndex))
                    .ToList();

                cardCount = defenderCards.Count;
                estimatedWins = EstimateDefenderWinsInSuit(suitMoves, defenderCards);
            }
            else
            {
                // For declarer, count only declarer's cards
                var declarerCards = suitMoves
                    .Where(m => m.Available && m.PlayerIndex == playerIndex)
                    .ToList();

                cardCount = declarerCards.Count;
                estimatedWins = EstimateDeclarerWinsInSuit(suitMoves, declarerCards);
            }

            return new SuitAnalysisResult
            {
                Suit = suit,
                HasStrongestCard = hasStrongestCard,
                EstimatedWins = estimatedWins,
                SuitLength = cardCount,
                HighestCard = highestCard
            };
        }

        /// <summary>
        /// Estimates how many tricks defenders can win in a suit
        /// Simple heuristic: count consecutive high cards
        /// </summary>
        private int EstimateDefenderWinsInSuit(List<PerfectCardMove> suitMoves, List<PerfectCardMove> defenderCards)
        {
            if (!defenderCards.Any())
                return 0;

            int wins = 0;

            // Count consecutive winners from the top
            foreach (var move in suitMoves)
            {
                if (!move.Available)
                    continue;

                if (_defenderIndices.Contains(move.PlayerIndex))
                {
                    wins++;
                }
                else
                {
                    // Hit a declarer card, stop counting
                    break;
                }
            }

            return wins;
        }

        /// <summary>
        /// Estimates how many tricks declarer can win in a suit
        /// </summary>
        private int EstimateDeclarerWinsInSuit(List<PerfectCardMove> suitMoves, List<PerfectCardMove> declarerCards)
        {
            if (!declarerCards.Any())
                return 0;

            int wins = 0;

            // Count consecutive winners from the top
            foreach (var move in suitMoves)
            {
                if (!move.Available)
                    continue;

                if (move.PlayerIndex == _declarerIndex)
                {
                    wins++;
                }
                else
                {
                    // Hit a defender card, stop counting
                    break;
                }
            }

            return wins;
        }

        // ==================== LEAD SELECTION ====================

        /// <summary>
        /// Selects the best lead strategy based on suit analysis
        /// </summary>
        private LeadStrategy SelectLeadStrategy(List<SuitAnalysisResult> analyses, int playerIndex)
        {
            // Priority 1: Lead from suit where we have the strongest card
            var strongestSuits = analyses
                .Where(a => a.HasStrongestCard && a.SuitLength > 0)
                .OrderByDescending(a => a.EstimatedWins)
                .ThenByDescending(a => a.SuitLength)
                .ToList();

            if (strongestSuits.Any())
            {
                var bestSuit = strongestSuits.First();
                var leadCard = GetStrongestCardInSuit(bestSuit.Suit, playerIndex);

                return new LeadStrategy
                {
                    LeadCard = leadCard,
                    Reason = "Leading from strongest suit"
                };
            }

            // Priority 2: Lead strongest card from longest suit
            var longestSuit = analyses
                .Where(a => a.SuitLength > 0)
                .OrderByDescending(a => a.SuitLength)
                .ThenByDescending(a => a.EstimatedWins)
                .FirstOrDefault();

            if (longestSuit != null)
            {
                var leadCard = GetStrongestCardInSuit(longestSuit.Suit, playerIndex);

                return new LeadStrategy
                {
                    LeadCard = leadCard,
                    Reason = "Leading from longest suit"
                };
            }

            // Fallback: Lead any available card
            var anyCard = _state.GetAvailableMovesForPlayer(playerIndex).FirstOrDefault();

            return new LeadStrategy
            {
                LeadCard = anyCard,
                Reason = "Fallback: any card"
            };
        }

        /// <summary>
        /// Gets the strongest available card in a suit for a player (or coalition)
        /// </summary>
        private PerfectCardMove GetStrongestCardInSuit(Suit suit, int playerIndex)
        {
            bool isDefender = (playerIndex != _declarerIndex);
            var suitMoves = _state.Moves[(int)suit];

            if (isDefender)
            {
                // For defenders, get highest card from either defender
                return suitMoves
                    .Where(m => m.Available && _defenderIndices.Contains(m.PlayerIndex))
                    .FirstOrDefault();
            }
            else
            {
                // For declarer, get highest declarer card
                return suitMoves
                    .Where(m => m.Available && m.PlayerIndex == playerIndex)
                    .FirstOrDefault();
            }
        }

        // ==================== FOLLOW SELECTION ====================

        /// <summary>
        /// Selects follow move for second player (one card already played)
        /// </summary>
        private PerfectCardMove SelectSecondPlayerFollow(
            PerfectCardMove leadMove, 
            int leadPlayer, 
            int currentPlayer, 
            List<SuitAnalysisResult> analyses)
        {
            var leadSuit = leadMove.Card.Suit;
            var suitMoves = _state.Moves[(int)leadSuit];

            bool currentIsDefender = (currentPlayer != _declarerIndex);
            bool leadIsDefender = (leadPlayer != _declarerIndex);
            bool isPartnerLead = currentIsDefender && leadIsDefender;

            // Check if we can follow suit
            var followCards = suitMoves
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();

            if (followCards.Any())
            {
                // Case 1: Partner led (both defenders)
                if (isPartnerLead)
                {
                    // Check if partner has strongest card
                    var analysis = analyses.First(a => a.Suit == leadSuit);

                    if (analysis.HasStrongestCard && analysis.HighestCard?.PlayerIndex == leadPlayer)
                    {
                        // Partner has strongest card → play smallest
                        return followCards.Last();
                    }
                    else
                    {
                        // Partner doesn't have strongest → play largest to help
                        return followCards.First();
                    }
                }
                else // Opponent led
                {
                    // Try to win with smallest winning card, or lose with smallest card
                    var winningCards = followCards
                        .Where(m => m.Card.Rank > leadMove.Card.Rank)
                        .ToList();

                    if (winningCards.Any())
                    {
                        return winningCards.Last(); // Smallest winning
                    }
                    else
                    {
                        return followCards.Last(); // Smallest losing
                    }
                }
            }

            // Cannot follow suit → must play trump or discard
            return SelectDiscardOrTrump(leadSuit, currentPlayer, analyses, isPartnerLead);
        }

        /// <summary>
        /// Selects follow move for third player (two cards already played)
        /// </summary>
        private PerfectCardMove SelectThirdPlayerFollow(
            PerfectCardMove leadMove,
            PerfectCardMove firstFollowMove,
            int leadPlayer,
            int currentPlayer,
            List<SuitAnalysisResult> analyses)
        {
            var leadSuit = leadMove.Card.Suit;
            var suitMoves = _state.Moves[(int)leadSuit];

            // Determine current winner
            int currentWinner = DetermineWinnerBetweenTwo(leadMove, firstFollowMove);

            bool currentIsDefender = (currentPlayer != _declarerIndex);
            bool winnerIsPartner = currentIsDefender && (currentWinner != _declarerIndex);

            // Check if we can follow suit
            var followCards = suitMoves
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();

            if (followCards.Any())
            {
                if (winnerIsPartner)
                {
                    // Partner winning → discard smallest
                    return followCards.Last();
                }
                else
                {
                    // Opponent winning → try to beat with smallest winning card
                    var currentWinningCard = (currentWinner == leadMove.PlayerIndex) ? leadMove : firstFollowMove;

                    var winningCards = followCards
                        .Where(m => m.Card.Rank > currentWinningCard.Card.Rank)
                        .ToList();

                    if (winningCards.Any())
                    {
                        return winningCards.Last(); // Smallest winning
                    }
                    else
                    {
                        return followCards.Last(); // Smallest losing
                    }
                }
            }

            // Cannot follow suit → must play trump or discard
            return SelectDiscardOrTrump(leadSuit, currentPlayer, analyses, winnerIsPartner);
        }

        /// <summary>
        /// Selects a discard or trump card when cannot follow suit
        /// </summary>
        private PerfectCardMove SelectDiscardOrTrump(
            Suit leadSuit, 
            int currentPlayer, 
            List<SuitAnalysisResult> analyses,
            bool partnerWinning)
        {
            // Check if we have trump cards
            var trumpMoves = _state.Moves[(int)_trumpSuit]
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();

            if (trumpMoves.Any() && leadSuit != _trumpSuit)
            {
                if (partnerWinning)
                {
                    // Partner winning, discard smallest trump
                    return trumpMoves.Last();
                }
                else
                {
                    // Opponent winning, play smallest trump to win
                    return trumpMoves.Last();
                }
            }

            // No trump or lead suit is trump → discard from weakest suit
            var weakestSuit = analyses
                .Where(a => a.Suit != leadSuit && a.SuitLength > 0)
                .OrderBy(a => a.EstimatedWins)
                .ThenBy(a => a.SuitLength)
                .FirstOrDefault();

            if (weakestSuit != null)
            {
                var discardMoves = _state.Moves[(int)weakestSuit.Suit]
                    .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                    .ToList();

                if (discardMoves.Any())
                {
                    return discardMoves.Last(); // Smallest card in weakest suit
                }
            }

            // Fallback: any available card
            return _state.GetAvailableMovesForPlayer(currentPlayer).First();
        }

        // ==================== WINNER DETERMINATION ====================

        /// <summary>
        /// Determines winner of a complete trick (3 cards)
        /// </summary>
        private int DetermineWinner(PerfectCardMove leadMove, PerfectCardMove follow1, PerfectCardMove follow2)
        {
            var leadSuit = leadMove.Card.Suit;

            // Collect all trump cards
            var trumpCards = new List<(PerfectCardMove move, int playerIndex)>();

            if (leadMove.Card.Suit == _trumpSuit)
                trumpCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == _trumpSuit)
                trumpCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == _trumpSuit)
                trumpCards.Add((follow2, follow2.PlayerIndex));

            // If any trump played, highest trump wins
            if (trumpCards.Any())
            {
                return trumpCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
            }

            // No trump → highest card in lead suit wins
            var leadSuitCards = new List<(PerfectCardMove move, int playerIndex)>();

            if (leadMove.Card.Suit == leadSuit)
                leadSuitCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == leadSuit)
                leadSuitCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == leadSuit)
                leadSuitCards.Add((follow2, follow2.PlayerIndex));

            return leadSuitCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
        }

        /// <summary>
        /// Determines winner between two cards (for third player decision)
        /// </summary>
        private int DetermineWinnerBetweenTwo(PerfectCardMove leadMove, PerfectCardMove followMove)
        {
            bool leadIsTrump = leadMove.Card.Suit == _trumpSuit;
            bool followIsTrump = followMove.Card.Suit == _trumpSuit;

            // Both trump → highest wins
            if (leadIsTrump && followIsTrump)
            {
                return leadMove.Card.Rank > followMove.Card.Rank 
                    ? leadMove.PlayerIndex 
                    : followMove.PlayerIndex;
            }

            // Follow is trump, lead is not
            if (followIsTrump && !leadIsTrump)
            {
                return followMove.PlayerIndex;
            }

            // Lead is trump, follow is not
            if (leadIsTrump && !followIsTrump)
            {
                return leadMove.PlayerIndex;
            }

            // Neither is trump → must follow suit
            if (followMove.Card.Suit != leadMove.Card.Suit)
            {
                return leadMove.PlayerIndex; // Didn't follow suit
            }

            // Same suit → highest rank wins
            return leadMove.Card.Rank > followMove.Card.Rank 
                ? leadMove.PlayerIndex 
                : followMove.PlayerIndex;
        }

        // ==================== HELPERS ====================

        private void MarkAsPlayed(PerfectCardMove move)
        {
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = false;
        }

        private void MarkAsAvailable(PerfectCardMove move)
        {
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = true;
        }

        private int GetNextPlayer(int currentPlayer)
        {
            return (currentPlayer + 1) % 3;
        }

        private int CountRemainingTricks()
        {
            return _state.GetAvailableMovesForPlayer(_state.CurrentPlayerIndex).Count();
        }

        private PerfPerfectGameState CloneState()
        {
            var clonedState = new PerfPerfectGameState(
                _state.GameMode,
                new List<string>(_state.Players),
                _state.TrumpSuit,
                _state.CurrentPlayerIndex,
                _state.LeaderPlayerIndex);

            // Clone all moves
            var allCards = new Dictionary<int, List<Card>>();

            for (int playerIndex = 0; playerIndex < 3; playerIndex++)
            {
                allCards[playerIndex] = _state.GetAvailableMovesForPlayer(playerIndex)
                    .Select(m => m.Card)
                    .ToList();
            }

            clonedState.SetupPlayerHands(allCards);

            return clonedState;
        }

        // ==================== HELPER CLASSES ====================

        private class SuitAnalysisResult
        {
            public Suit Suit { get; set; }
            public bool HasStrongestCard { get; set; }
            public int EstimatedWins { get; set; }
            public int SuitLength { get; set; }
            public PerfectCardMove? HighestCard { get; set; }
        }

        private class LeadStrategy
        {
            public PerfectCardMove LeadCard { get; set; }
            public string Reason { get; set; }
        }
    }
}