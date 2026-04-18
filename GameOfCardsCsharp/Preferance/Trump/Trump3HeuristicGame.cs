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
            
            return SelectLeadCard(currentPlayer, isCurrentDeclarer, suitAnalyses, _state);
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
                return SelectSecondPlayerFollow(leadMove, leadPlayer, currentPlayer, suitAnalyses, _state);
            }
            else
            {
                return SelectThirdPlayerFollow(leadMove, firstFollowMove, leadPlayer, currentPlayer, suitAnalyses, _state);
            }
        }

        /// <summary>
        /// Estimates the final Score3 by simulating the game using heuristics.
        /// This method plays out all remaining tricks using heuristic moves.
        /// </summary>
        public Score3 EstimateScore()
        {
            // Clone state to avoid modifying the original
            var simulationState = _state.Clone();

            // Track tricks won by each player during simulation
            int[] tricksWonDuringSimulation = new int[3];

            // Simulate all remaining tricks
            while (HasRemainingCards(simulationState))
            {
                // Play out one complete trick
                var trickWinner = SimulateTrick(simulationState);

                // Record the trick winner
                tricksWonDuringSimulation[trickWinner]++;
            }

            return Score3.FromThreePlayer(tricksWonDuringSimulation, _declarerIndex);
        }

        /// <summary>
        /// Simulates a single trick and returns the winner's index.
        /// Uses heuristic logic to select cards for each player.
        /// </summary>
        private int SimulateTrick(PerfPerfectGameState state)
        {
            int leadPlayer = state.CurrentPlayerIndex;

            // Get lead card using heuristics
            var leadMove = SelectLeadCard(leadPlayer, leadPlayer == _declarerIndex, state.AnalyzeAllSuits(_declarerIndex), state);

            // Mark lead card as played
            state.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = false;

            // Advance to next player
            state.CurrentPlayerIndex = GetNextPlayer(state.CurrentPlayerIndex);

            // Get first follow card
            var firstFollowMove = SelectFollowCard(leadMove, null, state);
            state.Moves[(int)firstFollowMove.Card.Suit][firstFollowMove.ListIndex].Available = false;

            // Advance to next player
            state.CurrentPlayerIndex = GetNextPlayer(state.CurrentPlayerIndex);

            // Get second follow card
            var secondFollowMove = SelectFollowCard(leadMove, firstFollowMove, state);
            state.Moves[(int)secondFollowMove.Card.Suit][secondFollowMove.ListIndex].Available = false;

            // Determine winner
            int winner = DetermineWinner(leadMove, firstFollowMove, secondFollowMove);

            // Set winner as next leader
            state.CurrentPlayerIndex = winner;

            return winner;
        }

        /// <summary>
        /// Selects a follow card using heuristic analysis (reuses existing logic)
        /// </summary>
        private PerfectCardMove SelectFollowCard(PerfectCardMove leadMove, PerfectCardMove? firstFollowMove, PerfPerfectGameState state)
        {
            int currentPlayer = state.CurrentPlayerIndex;
            int leadPlayer = leadMove.PlayerIndex;
            bool isSecondPlayer = (firstFollowMove == null);
            
            // Calculate suit analyses once for this trick
            var suitAnalyses = state.AnalyzeAllSuits(_declarerIndex);
            
            if (isSecondPlayer)
            {
                return SelectSecondPlayerFollow(leadMove, leadPlayer, currentPlayer, suitAnalyses, state);
            }
            else
            {
                return SelectThirdPlayerFollow(leadMove, firstFollowMove, leadPlayer, currentPlayer, suitAnalyses, state);
            }
        }

        /// <summary>
        /// Determines the winner of a trick given three cards
        /// </summary>
        private int DetermineWinner(PerfectCardMove leadMove, PerfectCardMove firstFollowMove, PerfectCardMove secondFollowMove)
        {
            // Collect all three moves
            var moves = new[] { leadMove, firstFollowMove, secondFollowMove };
            
            // Find trump cards
            var trumpCards = moves.Where(m => m.Card.Suit == _trumpSuit).ToArray();
            
            if (trumpCards.Any())
            {
                // At least one trump card - highest trump wins
                var winningMove = trumpCards.OrderByDescending(m => m.Card.Rank).First();
                return winningMove.PlayerIndex;
            }
            
            // No trumps - highest card in lead suit wins
            var leadSuit = leadMove.Card.Suit;
            var followingSuit = moves.Where(m => m.Card.Suit == leadSuit).ToArray();
            
            if (followingSuit.Any())
            {
                var winningMove = followingSuit.OrderByDescending(m => m.Card.Rank).First();
                return winningMove.PlayerIndex;
            }
            
            // Only lead card followed suit (others discarded) - lead wins
            return leadMove.PlayerIndex;
        }

        /// <summary>
        /// Checks if there are remaining cards to play
        /// </summary>
        private bool HasRemainingCards(PerfPerfectGameState state)
        {
            // Check if current player has any cards
            return state.GetAvailableMovesForPlayer(state.CurrentPlayerIndex).Any();
        }

        /// <summary>
        /// Gets the next player index in rotation
        /// </summary>
        private int GetNextPlayer(int currentPlayer)
        {
            return (currentPlayer + 1) % 3;
        }

        /// <summary>
        /// Selects optimal trump for Defender to play when cannot follow suit
        /// </summary>
        private PerfectCardMove SelectDefenderTrump(
            List<PerfectCardMove> trumpMoves,
            Suit leadSuit,
            bool isSecondPlayer,
            int currentDefender,
            PerfPerfectGameState state)
        {
            var declarerTrumpMoves = state.Moves[(int)_trumpSuit]
                .Where(m => m.Available && m.PlayerIndex == _declarerIndex)
                .ToList();

            if (isSecondPlayer)
            {
                // DEFENDER IS SECOND PLAYER - Declarer plays after
                
                // Check if declarer can follow suit
                var declarerLeadSuitMoves = state.Moves[(int)leadSuit]
                    .Where(m => m.Available && m.PlayerIndex == _declarerIndex)
                    .ToList();

                if (declarerLeadSuitMoves.Any())
                {
                    // Declarer CAN follow suit - won't trump
                    // Play smallest trump
                    return trumpMoves.Last();
                }
                else
                {
                    // Declarer CANNOT follow suit - will trump
                    if (!declarerTrumpMoves.Any())
                    {
                        // Declarer has no trump - play smallest
                        return trumpMoves.Last();
                    }

                    // Find declarer's highest trump
                    var highestDeclarerTrump = declarerTrumpMoves.First();

                    // Try to play smallest trump that beats declarer's highest
                    var beatingTrumps = trumpMoves
                        .Where(m => m.Card.Rank > highestDeclarerTrump.Card.Rank)
                        .ToList();

                    if (beatingTrumps.Any())
                    {
                        // Play smallest trump that beats declarer's highest
                        return beatingTrumps.Last();
                    }
                    else
                    {
                        // Can't beat - play LARGEST trump to force declarer to waste high trump
                        return trumpMoves.First();
                    }
                }
            }
            else
            {
                // DEFENDER IS LAST PLAYER (3rd position) - Declarer already played
                
                // Check what declarer played (need to look at trick history or state)
                // For now, check if declarer played trump in this trick
                var declarerPlayedTrump = CheckIfDeclarerPlayedTrumpInCurrentTrick();

                if (declarerPlayedTrump != null)
                {
                    // Declarer played trump - try to beat it
                    var beatingTrumps = trumpMoves
                        .Where(m => m.Card.Rank > declarerPlayedTrump.Card.Rank)
                        .ToList();

                    if (beatingTrumps.Any())
                    {
                        // Play smallest trump that beats declarer's
                        return beatingTrumps.Last();
                    }
                    else
                    {
                        // Can't beat - play smallest trump
                        return trumpMoves.Last();
                    }
                }
                else
                {
                    // Declarer didn't play trump - play smallest trump
                    return trumpMoves.Last();
                }
            }
        }

        /// <summary>
        /// Selects optimal trump for Declarer to play when cannot follow suit
        /// </summary>
        private PerfectCardMove SelectDeclarerTrump(
            List<PerfectCardMove> trumpMoves,
            Suit leadSuit,
            bool isSecondPlayer,
            int declarerIndex,
            PerfPerfectGameState state)
        {
            if (isSecondPlayer)
            {
                // DECLARER IS SECOND PLAYER - one Defender plays after
                
                // Determine which defender plays after
                int nextDefenderIndex = GetNextPlayerIndex(state.CurrentPlayerIndex);

                // Check if next defender can follow suit
                var defenderLeadSuitMoves = state.Moves[(int)leadSuit]
                    .Where(m => m.Available && m.PlayerIndex == nextDefenderIndex)
                    .ToList();

                if (defenderLeadSuitMoves.Any())
                {
                    // Defender CAN follow suit - won't trump
                    // Play smallest trump
                    return trumpMoves.Last();
                }
                else
                {
                    // Defender CANNOT follow suit - will trump
                    var defenderTrumpMoves = state.Moves[(int)_trumpSuit]
                        .Where(m => m.Available && m.PlayerIndex == nextDefenderIndex)
                        .ToList();

                    if (!defenderTrumpMoves.Any())
                    {
                        // Defender has no trump - play smallest
                        return trumpMoves.Last();
                    }

                    // Find defender's highest trump
                    var highestDefenderTrump = defenderTrumpMoves.First();

                    // Try to play smallest trump that beats defender's highest
                    var beatingTrumps = trumpMoves
                        .Where(m => m.Card.Rank > highestDefenderTrump.Card.Rank)
                        .ToList();

                    if (beatingTrumps.Any())
                    {
                        // Play smallest trump that beats defender's highest
                        return beatingTrumps.Last();
                    }
                    else
                    {
                        // Can't beat - play LARGEST trump to force defender to waste high trump
                        return trumpMoves.First();
                    }
                }
            }
            else
            {
                // DECLARER IS LAST PLAYER (3rd position) - check what defenders played
                
                var defendersPlayedTrump = CheckIfAnyDefenderPlayedTrumpInCurrentTrick();

                if (defendersPlayedTrump != null)
                {
                    // Defender played trump - try to beat it
                    var beatingTrumps = trumpMoves
                        .Where(m => m.Card.Rank > defendersPlayedTrump.Card.Rank)
                        .ToList();

                    if (beatingTrumps.Any())
                    {
                        // Play smallest trump that beats defender's
                        return beatingTrumps.Last();
                    }
                    else
                    {
                        // Can't beat - play smallest trump
                        return trumpMoves.Last();
                    }
                }
                else
                {
                    // No defender played trump - play smallest trump
                    return trumpMoves.Last();
                }
            }
        }

        /// <summary>
        /// Checks if declarer played trump in the current trick
        /// Returns the trump card played, or null if didn't play trump
        /// </summary>
        private PerfectCardMove? CheckIfDeclarerPlayedTrumpInCurrentTrick()
        {
            // This requires tracking trick state - for now return null
            // In a full implementation, you'd track cards played in current trick
            // TODO: Implement proper trick tracking
            return null;
        }

        /// <summary>
        /// Checks if any defender played trump in the current trick
        /// Returns the highest trump card played by defenders, or null if no trump played
        /// </summary>
        private PerfectCardMove? CheckIfAnyDefenderPlayedTrumpInCurrentTrick()
        {
            // This requires tracking trick state - for now return null
            // TODO: Implement proper trick tracking
            return null;
        }

        /// <summary>
        /// Returns the best lead card using heuristic analysis
        /// </summary>
        private PerfectCardMove SelectLeadCard(
            int currentPlayer, 
            bool isCurrentDeclarer, 
            List<SuitAnalysis3Result> suitAnalyses,
            PerfPerfectGameState state)
        {
            // Declarer logic: simple, no partner coordination
            if (isCurrentDeclarer)
            {
                return SelectDeclarerLeadCard(currentPlayer, suitAnalyses, state);
            }

            // Defender logic: coordinate with partner
            return SelectDefenderLeadCard(currentPlayer, suitAnalyses, state);
        }

        private PerfectCardMove SelectDeclarerLeadCard(int declarerIndex, List<SuitAnalysis3Result> suitAnalyses, PerfPerfectGameState state)
        {
            // Filter suits where declarer has cards
            var availableSuits = suitAnalyses
                .Where(a => a.CardsPerPlayer[declarerIndex] > 0)
                .ToList();

            if (!availableSuits.Any())
            {
                // Fallback: play any available card
                return state.GetAvailableMovesForPlayer(declarerIndex).First();
            }

            // Priority 1: Lead from suits where leading doesn't matter (same result either way)
            // These are "safe" suits where declarer doesn't lose first-mover advantage
            var safeSuits = availableSuits
                .Where(a => a.DeclarerWinsIfDeclarerLeads == a.DeclarerWinsIfDefenderLeads)
                .ToList();

            if (safeSuits.Any())
            {
                // Among safe suits, prefer those where declarer has strongest card
                var controlledSafeSuits = safeSuits
                    .Where(a => a.StrongestCardOwner == declarerIndex)
                    .OrderByDescending(a => a.DeclarerWinsIfDeclarerLeads)
                    .ThenByDescending(a => a.TotalCardsInSuit)
                    .ToList();

                if (controlledSafeSuits.Any())
                {
                    var bestSuit = controlledSafeSuits.First();
                    return bestSuit.StrongestCard!;
                }

                // Among safe suits without control, prefer longest suit
                var longestSafeSuit = safeSuits
                    .OrderByDescending(a => a.CardsPerPlayer[declarerIndex])
                    .ThenByDescending(a => a.DeclarerWinsIfDeclarerLeads)
                    .First();

                return GetHighestCardInSuit(longestSafeSuit.Suit, declarerIndex, false, state);
            }

            // Priority 2: No safe suits - lead from suits where declarer benefits from leading first
            var advantageousSuits = availableSuits
                .Where(a => a.DeclarerWinsIfDeclarerLeads > a.DeclarerWinsIfDefenderLeads)
                .OrderByDescending(a => a.DeclarerWinsIfDeclarerLeads - a.DeclarerWinsIfDefenderLeads) // Biggest advantage
                .ThenByDescending(a => a.DeclarerWinsIfDeclarerLeads)
                .ToList();

            if (advantageousSuits.Any())
            {
                var bestSuit = advantageousSuits.First();
                
                // If declarer has strongest card, lead it
                if (bestSuit.StrongestCardOwner == declarerIndex && bestSuit.StrongestCard != null)
                {
                    return bestSuit.StrongestCard;
                }
                
                return GetHighestCardInSuit(bestSuit.Suit, declarerIndex, false, state);
            }

            // Priority 3: All suits are disadvantageous - minimize loss
            var leastBadSuit = availableSuits
                .OrderBy(a => a.DeclarerWinsIfDefenderLeads - a.DeclarerWinsIfDeclarerLeads) // Smallest disadvantage
                .ThenByDescending(a => a.CardsPerPlayer[declarerIndex])
                .First();

            return GetHighestCardInSuit(leastBadSuit.Suit, declarerIndex, false, state);
        }

        private PerfectCardMove SelectDefenderLeadCard(int currentDefender, List<SuitAnalysis3Result> suitAnalyses, PerfPerfectGameState state)
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
                return state.GetAvailableMovesForPlayer(currentDefender).First();
            }

            // Priority 1: Lead from suits where leading doesn't matter (same result either way)
            // These are "safe" suits where defender doesn't lose first-mover advantage
            var safeSuits = availableSuits
                .Where(a => a.DefenderWinsIfDeclarerLeads == a.DefenderWinsIfDefenderLeads)
                .ToList();

            if (safeSuits.Any())
            {
                // Among safe suits, prefer those where defenders control
                var controlledSafeSuits = safeSuits
                    .Where(a => _defenderIndices.Contains(a.StrongestCardOwner))
                    .OrderByDescending(a => a.DefenderWinsIfDefenderLeads)
                    .ThenByDescending(a => a.TotalCardsInSuit)
                    .ToList();

                if (controlledSafeSuits.Any())
                {
                    var bestSuit = controlledSafeSuits.First();

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
                        var smallestCard = GetSmallestCardInSuit(bestSuit.Suit, currentDefender, state);
                        
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

                // Among safe suits without control, prefer longest suit
                var longestSafeSuit = safeSuits
                    .OrderByDescending(a => a.CardsPerPlayer[currentDefender])
                    .ThenByDescending(a => a.DefenderWinsIfDefenderLeads)
                    .First();

                return GetHighestCardInSuit(longestSafeSuit.Suit, currentDefender, true, state);
            }

            // Priority 2: No safe suits - lead from suits where defenders benefit from leading first
            var advantageousSuits = availableSuits
                .Where(a => a.DefenderWinsIfDefenderLeads > a.DefenderWinsIfDeclarerLeads)
                .OrderByDescending(a => a.DefenderWinsIfDefenderLeads - a.DefenderWinsIfDeclarerLeads) // Biggest advantage
                .ThenByDescending(a => a.DefenderWinsIfDefenderLeads)
                .ToList();

            if (advantageousSuits.Any())
            {
                var bestSuit = advantageousSuits.First();
                
                // If any defender has strongest card
                if (_defenderIndices.Contains(bestSuit.StrongestCardOwner))
                {
                    if (bestSuit.StrongestCardOwner == currentDefender && bestSuit.StrongestCard != null)
                    {
                        return bestSuit.StrongestCard;
                    }
                    else if (bestSuit.StrongestCardOwner == partnerDefender)
                    {
                        // Signal to partner with smallest card
                        var smallestCard = GetSmallestCardInSuit(bestSuit.Suit, currentDefender, state);
                        
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
                
                return GetHighestCardInSuit(bestSuit.Suit, currentDefender, true, state);
            }

            // Priority 3: All suits are disadvantageous - minimize loss
            var leastBadSuit = availableSuits
                .OrderBy(a => a.DefenderWinsIfDeclarerLeads - a.DefenderWinsIfDefenderLeads) // Smallest disadvantage
                .ThenByDescending(a => a.CardsPerPlayer[currentDefender])
                .First();

            return GetHighestCardInSuit(leastBadSuit.Suit, currentDefender, true, state);
        }

        // ==================== FOLLOW SELECTION ====================

        private PerfectCardMove SelectSecondPlayerFollow(
            PerfectCardMove leadMove, 
            int leadPlayer, 
            int currentPlayer, 
            List<SuitAnalysis3Result> suitAnalyses,
            PerfPerfectGameState state)
        {
            var leadSuit = leadMove.Card.Suit;
            var suitMoves = state.Moves[(int)leadSuit];
            
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
            return SelectDiscardOrTrump(leadSuit, currentPlayer, suitAnalyses, isPartnerLead, state);
        }

        private PerfectCardMove SelectThirdPlayerFollow(
            PerfectCardMove leadMove,
            PerfectCardMove firstFollowMove,
            int leadPlayer,
            int currentPlayer,
            List<SuitAnalysis3Result> suitAnalyses,
            PerfPerfectGameState state)
        {
            var leadSuit = leadMove.Card.Suit;
            var suitMoves = state.Moves[(int)leadSuit];
            
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
            
            return SelectDiscardOrTrump(leadSuit, currentPlayer, suitAnalyses, partnerWinning, state);
        }

        private PerfectCardMove SelectDiscardOrTrump(
            Suit leadSuit, 
            int currentPlayer, 
            List<SuitAnalysis3Result> suitAnalyses,
            bool partnerWinning,
            PerfPerfectGameState state)
        {
            bool isDefender = (currentPlayer != _declarerIndex);
            
            // Check if we can follow suit first
            var followSuitMoves = state.Moves[(int)leadSuit]
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();
            
            if (followSuitMoves.Any())
            {
                // Should not reach here, but handle it
                return followSuitMoves.Last();
            }
            
            // Cannot follow suit
            var trumpMoves = state.Moves[(int)_trumpSuit]
                .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                .ToList();
            
            // CASE 1: Lead suit IS trump - we cannot follow with that specific trump
            if (leadSuit == _trumpSuit)
            {
                // Must discard non-trump OR another trump we don't have
                return SelectTrumpLeadDiscard(currentPlayer, suitAnalyses, isDefender, state);
            }
            
            // CASE 2: Lead suit is NOT trump - MUST play trump if we have it
            if (trumpMoves.Any())
            {
                // Determine our position in the trick
                int leadPlayerIndex = state.LeaderPlayerIndex;
                bool isSecondPlayer = (state.CurrentPlayerIndex == GetNextPlayerIndex(leadPlayerIndex));
                
                if (isDefender)
                {
                    return SelectDefenderTrump(trumpMoves, leadSuit, isSecondPlayer, currentPlayer, state);
                }
                else
                {
                    return SelectDeclarerTrump(trumpMoves, leadSuit, isSecondPlayer, currentPlayer, state);
                }
            }
            
            // CASE 3: No trump available - discard from another suit
            var nonTrumpSuits = suitAnalyses
                .Where(a => a.Suit != leadSuit && a.Suit != _trumpSuit && a.CardsPerPlayer[currentPlayer] > 0)
                .ToList();

            if (nonTrumpSuits.Any())
            {
                var discardSuit = nonTrumpSuits
                    .OrderByDescending(a => a.CardsPerPlayer[currentPlayer])
                    .ThenBy(a => a.DefenderWinsIfDefenderLeads)
                    .First();

                var discardMoves = state.Moves[(int)discardSuit.Suit]
                    .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                    .ToList();

                if (discardMoves.Any())
                {
                    return discardMoves.Last(); // Smallest
                }
            }
            
            // Fallback
            return state.GetAvailableMovesForPlayer(currentPlayer).First();
        }

        /// <summary>
        /// Gets the index of the next player in rotation (wraps around at 3)
        /// </summary>
        private int GetNextPlayerIndex(int currentPlayerIndex)
        {
            return (currentPlayerIndex + 1) % 3;
        }

        /// <summary>
        /// Selects a card to discard when lead suit is trump and we cannot follow
        /// </summary>
        private PerfectCardMove SelectTrumpLeadDiscard(
            int currentPlayer, 
            List<SuitAnalysis3Result> suitAnalyses,
            bool isDefender,
            PerfPerfectGameState state)
        {
            // Get all non-trump suits where current player has cards
            var nonTrumpSuits = suitAnalyses
                .Where(a => a.Suit != _trumpSuit && a.CardsPerPlayer[currentPlayer] > 0)
                .ToList();

            if (!nonTrumpSuits.Any())
            {
                // No non-trump cards - must discard smallest trump
                var trumpMoves = state.Moves[(int)_trumpSuit]
                    .Where(m => m.Available && m.PlayerIndex == currentPlayer)
                    .ToList();
                return trumpMoves.Last(); // Last = smallest
            }

            // Discard from the suit with worst prospects
            var worstSuit = nonTrumpSuits
                .OrderBy(a => isDefender ? a.DefenderWinsIfDefenderLeads : a.DeclarerWinsIfDeclarerLeads)
                .ThenBy(a => a.CardsPerPlayer[currentPlayer])
                .First();

            // Return smallest card in that suit
            return GetSmallestCardInSuit(worstSuit.Suit, currentPlayer, state);
        }

        /// <summary>
        /// Gets the smallest card in a suit for the specified player
        /// </summary>
        private PerfectCardMove GetSmallestCardInSuit(Suit suit, int playerIndex, PerfPerfectGameState state)
        {
            var suitMoves = state.Moves[(int)suit]
                .Where(m => m.Available && m.PlayerIndex == playerIndex)
                .ToList();
            
            return suitMoves.Last(); // Last element = smallest rank
        }

        /// <summary>
        /// Gets the highest card in a suit for the specified player
        /// </summary>
        /// <param name="suit">The suit to search in</param>
        /// <param name="playerIndex">The player index</param>
        /// <param name="allowSignaling">Whether this is for a defender who might want to signal to partner</param>
        /// <returns>The highest available card in the suit</returns>
        private PerfectCardMove GetHighestCardInSuit(Suit suit, int playerIndex, bool allowSignaling, PerfPerfectGameState state)
        {
            var suitMoves = state.Moves[(int)suit]
                .Where(m => m.Available && m.PlayerIndex == playerIndex)
                .ToList();

            if (!suitMoves.Any())
            {
                throw new InvalidOperationException($"Player {playerIndex} has no cards in {suit}");
            }

            // Cards are sorted from highest to lowest, so first element is highest
            return suitMoves.First();
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