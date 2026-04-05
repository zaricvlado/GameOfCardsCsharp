using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Trump
{
    /// <summary>
    /// Heuristic-based strategy for 3-player Trump games.
    /// Uses centralized suit analysis from PerfPerfectGameState to make decisions.
    /// </summary>
    public class Trump3HeuristicGame
    {
        private readonly PerfPerfectGameState _state;
        private readonly Suit _trumpSuit;
        private readonly int _declarerIndex;
        private readonly int[] _defenderIndices;

        // Signal for partner coordination
        private PartnerSignal? _lastLeadSignal;

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

            _lastLeadSignal = null;
        }

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
            bool isCurrentDeclarer = (currentPlayer == _declarerIndex);
            
            // Analyze all suits ONCE using centralized state analysis
            var suitAnalyses = _state.AnalyzeAllSuits(_declarerIndex);
            
            // Clear previous signal (new trick starting)
            _lastLeadSignal = null;
            
            return SelectLeadCard(currentPlayer, isCurrentDeclarer, suitAnalyses);
        }

        /// <summary>
        /// Returns the best follow card using heuristic rules.
        /// Requires pre-calculated suit analyses to avoid redundant computation.
        /// </summary>
        public PerfectCardMove BestFollowCard(
            PerfectCardMove leadMove, 
            PerfectCardMove? firstFollowMove = null,
            List<SuitAnalysis3Result>? suitAnalyses = null)
        {
            int currentPlayer = _state.CurrentPlayerIndex;
            int leadPlayer = leadMove.PlayerIndex;
            bool isSecondPlayer = (firstFollowMove == null);
            
            // Use provided analyses or calculate if not provided
            suitAnalyses ??= _state.AnalyzeAllSuits(_declarerIndex);
            
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
        /// Estimates the final Score3 by playing the entire game to completion using heuristics.
        /// Creates a copy of the current state and simulates all remaining tricks.
        /// </summary>
        public Score3 EstimateScore()
        {
            // Clone the state to simulate without affecting the original
            var simulationState = CloneState(_state);
            var simulationGame = new Trump3HeuristicGame(simulationState, _declarerIndex);
            
            // Track tricks won by each player
            var tricksWon = new int[3];
            
            // Play the game to completion
            while (HasMovesRemaining(simulationState))
            {
                // Lead card
                var leadMove = simulationGame.BestLeadCard();
                simulationState.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = false;
                
                int leadPlayer = simulationState.CurrentPlayerIndex;
                simulationState.AdvanceTurn();
                
                // Second player follows
                var secondMove = simulationGame.BestFollowCard(leadMove);
                simulationState.Moves[(int)secondMove.Card.Suit][secondMove.ListIndex].Available = false;
                simulationState.AdvanceTurn();
                
                // Third player follows
                var thirdMove = simulationGame.BestFollowCard(leadMove, secondMove);
                simulationState.Moves[(int)thirdMove.Card.Suit][thirdMove.ListIndex].Available = false;
                
                // Determine winner of this trick
                int winnerId = DetermineWinner(leadMove, secondMove, thirdMove);
                tricksWon[winnerId]++;
                
                // Winner leads next trick
                simulationState.CurrentPlayerIndex = winnerId;
            }
            
            // Return the final score based on tricks won
            return Score3.FromThreePlayer(tricksWon, _declarerIndex);
        }
        
        /// <summary>
        /// Clones the PerfPerfectGameState for simulation purposes
        /// </summary>
        private static PerfPerfectGameState CloneState(PerfPerfectGameState original)
        {
            var clone = new PerfPerfectGameState(
                original.GameMode, 
                new List<string>(original.Players),
                original.TrumpSuit,
                original.CurrentPlayerIndex,
                original.LeaderPlayerIndex);
            
            // Clone all moves
            for (int suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                foreach (var move in original.Moves[suitIndex])
                {
                    clone.Moves[suitIndex].Add(new PerfectCardMove(
                        move.Card,
                        move.PlayerIndex,
                        move.ListIndex,
                        move.Available
                    ));
                }
            }
            
            return clone;
        }
        
        /// <summary>
        /// Checks if there are any moves remaining in the game
        /// </summary>
        private static bool HasMovesRemaining(PerfPerfectGameState state)
        {
            return state.Moves.Any(suitMoves => suitMoves.Any(m => m.Available));
        }
        
        /// <summary>
        /// Determines the winner of a trick (highest trump, or highest card in lead suit)
        /// </summary>
        private int DetermineWinner(PerfectCardMove leadMove, PerfectCardMove follow1, PerfectCardMove follow2)
        {
            var trumpSuit = _trumpSuit;
            var leadSuit = leadMove.Card.Suit;
            
            // Check for trump cards
            var trumpCards = new List<(PerfectCardMove move, int playerIndex)>();
            
            if (leadMove.Card.Suit == trumpSuit)
                trumpCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == trumpSuit)
                trumpCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == trumpSuit)
                trumpCards.Add((follow2, follow2.PlayerIndex));
            
            // If there are trump cards, highest trump wins
            if (trumpCards.Any())
            {
                return trumpCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
            }
            
            // Otherwise, highest card in lead suit wins
            var leadSuitCards = new List<(PerfectCardMove move, int playerIndex)>();
            
            if (leadMove.Card.Suit == leadSuit)
                leadSuitCards.Add((leadMove, leadMove.PlayerIndex));
            if (follow1.Card.Suit == leadSuit)
                leadSuitCards.Add((follow1, follow1.PlayerIndex));
            if (follow2.Card.Suit == leadSuit)
                leadSuitCards.Add((follow2, follow2.PlayerIndex));
            
            return leadSuitCards.OrderByDescending(t => t.move.Card.Rank).First().playerIndex;
        }

        // ==================== LEAD SELECTION ====================

        private PerfectCardMove SelectLeadCard(
            int currentPlayer, 
            bool isCurrentDeclarer, 
            List<SuitAnalysis3Result> suitAnalyses)
        {
            // Declarer logic: simple, no partner coordination
            if (isCurrentDeclarer)
            {
                return SelectDeclarerLeadCard(currentPlayer, suitAnalyses);
            }

            // Defender logic: coordinate with partner
            return SelectDefenderLeadCard(currentPlayer, suitAnalyses);
        }

        private PerfectCardMove SelectDeclarerLeadCard(int declarerIndex, List<SuitAnalysis3Result> suitAnalyses)
        {
            // Filter suits where declarer has cards
            var availableSuits = suitAnalyses
                .Where(a => a.CardsPerPlayer[declarerIndex] > 0)
                .ToList();

            if (!availableSuits.Any())
            {
                // Fallback: play any available card
                return _state.GetAvailableMovesForPlayer(declarerIndex).First();
            }

            // Priority 1: Lead from suit where declarer has strongest card
            var controlledSuits = availableSuits
                .Where(a => a.StrongestCardOwner == declarerIndex)
                .OrderByDescending(a => a.DeclarerWins)
                .ThenByDescending(a => a.TotalCardsInSuit)
                .ToList();

            if (controlledSuits.Any())
            {
                var bestSuit = controlledSuits.First();
                return bestSuit.StrongestCard; // Play the strongest card
            }

            // Priority 2: Lead strongest card from longest suit
            var longestSuit = availableSuits
                .OrderByDescending(a => a.CardsPerPlayer[declarerIndex])
                .ThenByDescending(a => a.DeclarerWins)
                .First();

            return GetHighestCardInSuit(longestSuit.Suit, declarerIndex, false);
        }

        private PerfectCardMove SelectDefenderLeadCard(int currentDefender, List<SuitAnalysis3Result> suitAnalyses)
        {
            int partnerDefender = _defenderIndices.First(d => d != currentDefender);

            // Filter suits where current defender has cards
            var availableSuits = suitAnalyses
                .Where(a => a.CardsPerPlayer[currentDefender] > 0)
                .ToList();

            if (!availableSuits.Any())
            {
                // TO DO this is an error INVALID STATE,
                // there must be at least one suit where player has cards

                // Fallback: play any available card
                return _state.GetAvailableMovesForPlayer(currentDefender).First();
            }

            // Priority 1: Suits where defenders control (either defender has strongest)
            var controlledSuits = availableSuits
                .Where(a => _defenderIndices.Contains(a.StrongestCardOwner))
                .OrderByDescending(a => a.DefenderWins)
                .ThenByDescending(a => a.TotalCardsInSuit)
                .ToList();

            if (controlledSuits.Any())
            {
                var bestSuit = controlledSuits.First();

                // Case 1: Current defender has the strongest card
                if (bestSuit.StrongestCardOwner == currentDefender && bestSuit.StrongestCard != null)
                {
                    // Lead with strongest card
                    return bestSuit.StrongestCard;
                }
                
                // Case 2: Partner has the strongest card
                if (bestSuit.StrongestCardOwner == partnerDefender)
                {
                    // Signal to partner: Lead with SMALLEST card
                    var smallestCard = GetSmallestCardInSuit(bestSuit.Suit, currentDefender);
                    
                    // Set signal for partner
                    _lastLeadSignal = new PartnerSignal
                    {
                        LeadSuit = bestSuit.Suit,
                        LeaderIndex = currentDefender,
                        IsSmallestCardSignal = true,
                        PartnerHasControl = true
                    };
                    
                    return smallestCard;
                }
            }

            // Priority 2: No controlled suits - lead from longest suit
            var longestSuit = availableSuits
                .OrderByDescending(a => a.CardsPerPlayer[currentDefender])
                .ThenByDescending(a => a.DefenderWins)
                .First();

            return GetHighestCardInSuit(longestSuit.Suit, currentDefender, true);
        }

        // ==================== FOLLOW SELECTION ====================

        private PerfectCardMove SelectSecondPlayerFollow(
            PerfectCardMove leadMove, 
            int leadPlayer, 
            int currentPlayer, 
            List<SuitAnalysis3Result> suitAnalyses)
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
                // Check for partner signal
                if (isPartnerLead && _lastLeadSignal != null && 
                    _lastLeadSignal.LeadSuit == leadSuit && 
                    _lastLeadSignal.IsSmallestCardSignal)
                {
                    // Partner signaled with smallest card
                    // We need to check: has declarer already played?
                    
                    // In second player position after partner leads:
                    // - If there are 3 players and partner led, we're player 2
                    // - Declarer could be player 1 (already played) or player 3 (not yet)
                    
                    // Since partner led and we're second, declarer must be third player
                    // Therefore: Declarer has NOT played yet
                    
                    // Play highest card to force declarer's hand
                    return followCards.First();
                }

                // Normal follow logic (no signal)
                if (isPartnerLead)
                {
                    // Check if partner has strongest card in suit
                    var analysis = suitAnalyses.First(a => a.Suit == leadSuit);
                    
                    if (analysis.StrongestCardOwner == leadPlayer)
                    {
                        // Partner has strongest → play smallest
                        return followCards.Last();
                    }
                    else
                    {
                        // Partner doesn't have strongest → play largest to help
                        return followCards.First();
                    }
                }
                else // Opponent (declarer) led
                {
                    // Declarer led - check for signal from previous trick
                    // If partner signaled last trick and declarer just played, respond optimally
                    if (_lastLeadSignal != null && 
                        _lastLeadSignal.LeadSuit == leadSuit &&
                        _lastLeadSignal.IsSmallestCardSignal &&
                        leadPlayer == _declarerIndex)
                    {
                        // Declarer led after partner's signal on previous trick
                        // Play smallest card that beats declarer
                        var beatingCards = followCards
                            .Where(m => m.Card.Rank > leadMove.Card.Rank)
                            .ToList();
                        
                        if (beatingCards.Any())
                        {
                            return beatingCards.Last(); // Smallest winning
                        }
                        else
                        {
                            return followCards.Last(); // Can't beat, play smallest
                        }
                    }
                    
                    // Standard declarer-led response
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
            return SelectDiscardOrTrump(leadSuit, currentPlayer, suitAnalyses, isPartnerLead);
        }

        private PerfectCardMove SelectThirdPlayerFollow(
            PerfectCardMove leadMove,
            PerfectCardMove firstFollowMove,
            int leadPlayer,
            int currentPlayer,
            List<SuitAnalysis3Result> suitAnalyses)
        {
            var leadSuit = leadMove.Card.Suit;
            var suitMoves = _state.Moves[(int)leadSuit];
            
            bool currentIsDefender = (currentPlayer != _declarerIndex);
            int partnerIndex = currentIsDefender ? _defenderIndices.First(d => d != currentPlayer) : -1;
            
            // Check if we can follow suit
            var followCards = suitMoves
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();
            
            if (followCards.Any())
            {
                // Check for partner signal (partner led with smallest from controlled suit)
                if (currentIsDefender && _lastLeadSignal != null &&
                    _lastLeadSignal.LeadSuit == leadSuit &&
                    _lastLeadSignal.IsSmallestCardSignal &&
                    _lastLeadSignal.LeaderIndex == partnerIndex)
                {
                    // Partner signaled → respond based on whether declarer already played
                    int secondPlayerIndex = firstFollowMove.PlayerIndex;
                    bool declarerPlayedSecond = (secondPlayerIndex == _declarerIndex);
                    
                    if (declarerPlayedSecond)
                    {
                        // Declarer already played → beat declarer's card with smallest winning
                        var beatingCards = followCards
                            .Where(m => m.Card.Rank > firstFollowMove.Card.Rank)
                            .ToList();
                        
                        if (beatingCards.Any())
                        {
                            return beatingCards.Last(); // Smallest that beats declarer
                        }
                        else
                        {
                            // Can't beat declarer - play smallest
                            return followCards.Last();
                        }
                    }
                    else
                    {
                        // Declarer led (is first player) → beat declarer's lead with smallest winning
                        var beatingCards = followCards
                            .Where(m => m.Card.Rank > leadMove.Card.Rank)
                            .ToList();
                        
                        if (beatingCards.Any())
                        {
                            return beatingCards.Last(); // Smallest that beats declarer
                        }
                        else
                        {
                            // Can't beat declarer - play smallest
                            return followCards.Last();
                        }
                    }
                }
                
                // Normal third player logic (no signal)
                int currentWinner = DetermineWinnerBetweenTwo(leadMove, firstFollowMove);
                bool winnerIsPartner = currentIsDefender && (currentWinner != _declarerIndex);
                
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
            bool partnerWinning = currentIsDefender && 
                (DetermineWinnerBetweenTwo(leadMove, firstFollowMove) != _declarerIndex);
            
            return SelectDiscardOrTrump(leadSuit, currentPlayer, suitAnalyses, partnerWinning);
        }

        private PerfectCardMove SelectDiscardOrTrump(
            Suit leadSuit, 
            int currentPlayer, 
            List<SuitAnalysis3Result> suitAnalyses,
            bool partnerWinning)
        {
            bool isDefender = (currentPlayer != _declarerIndex);
            
            // Check if we have trump cards
            var trumpMoves = _state.Moves[(int)_trumpSuit]
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();
            
            if (trumpMoves.Any() && leadSuit != _trumpSuit)
            {
                // Play smallest trump
                return trumpMoves.Last();
            }
            
            // No trump or lead suit is trump → discard from longest suit
            var discardSuits = suitAnalyses
                .Where(a => a.Suit != leadSuit && a.CardsPerPlayer[currentPlayer] > 0)
                .ToList();

            if (!discardSuits.Any())
            {
                // Fallback: any available card
                return _state.GetAvailableMovesForPlayer(currentPlayer).First();
            }

            // Discard from longest suit
            var discardSuit = discardSuits
                .OrderByDescending(a => a.CardsPerPlayer[currentPlayer])
                .ThenBy(a => isDefender ? a.DefenderWins : a.DeclarerWins)
                .First();
            
            var discardMoves = _state.Moves[(int)discardSuit.Suit]
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();
            
            if (discardMoves.Any())
            {
                return discardMoves.Last(); // Smallest card
            }
            
            // Ultimate fallback
            return _state.GetAvailableMovesForPlayer(currentPlayer).First();
        }

        // ==================== HELPER METHODS ====================

        private PerfectCardMove GetHighestCardInSuit(Suit suit, int playerIndex, bool isDefender)
        {
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

        private PerfectCardMove GetSmallestCardInSuit(Suit suit, int playerIndex)
        {
            var suitMoves = _state.Moves[(int)suit];
            
            return suitMoves
                .Where(m => m.Available && m.PlayerIndex == playerIndex)
                .LastOrDefault();
        }

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
                return leadMove.PlayerIndex;
            }
            
            // Same suit → highest rank wins
            return leadMove.Card.Rank > followMove.Card.Rank 
                ? leadMove.PlayerIndex 
                : followMove.PlayerIndex;
        }

        // ==================== PARTNER SIGNALING ====================

        private class PartnerSignal
        {
            public Suit LeadSuit { get; set; }
            public int LeaderIndex { get; set; }
            public bool IsSmallestCardSignal { get; set; }
            public bool PartnerHasControl { get; set; }
        }
    }
}