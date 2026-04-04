using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Trump
{
    /// <summary>
    /// Perfect information game for 2-player Trump.
    /// Uses recursive evaluation to determine optimal moves.
    /// </summary>
    public class Trump2PerfectGame
    {
        private readonly PerfPerfectGameState _state;
        private readonly Suit _trumpSuit;
        private bool _exploreAllOptions;

        public PerfPerfectGameState State => _state;

        public Trump2PerfectGame(PerfPerfectGameState state)
        {
            if (state.Players.Count != 2)
            {
                throw new ArgumentException("Trump2PerfectGame only supports 2 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Trump)
            {
                throw new ArgumentException("Trump2PerfectGame requires Trump game mode", nameof(state));
            }

            if (state.TrumpSuit == TrumpSuit.None)
            {
                throw new ArgumentException("Trump suit must be specified for Trump games", nameof(state));
            }

            _state = state;
            _trumpSuit = ConvertTrumpSuitToSuit(state.TrumpSuit);
            _exploreAllOptions = false;
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
        /// Calculates the best lead cards for the current player in a 2-player game.
        /// Returns a list of PerfectCardMove with ExpectedTricks populated.
        /// Multiple moves are returned when they result in the same optimal score.
        /// The list is extended to include equivalent smaller cards in the same suit.
        /// </summary>
        public List<PerfectCardMove> BestLeadCard()
        {
            int handsLeft = CountRemainingHands();
            
            // Enable full exploration for endgame (7 cards or fewer)
            _exploreAllOptions = handsLeft <= 7;
            
            var candidates = _state.GetCandidateMovesForCurrentPlayer().ToList();

            if (!candidates.Any())
            {
                throw new InvalidOperationException("No available moves for current player");
            }

            var bestMoves = new List<PerfectCardMove>();
            Score2 bestScore = CalculateScore(candidates[0], handsLeft);
            bestMoves.Add(candidates[0].WithExpectedTricks(bestScore.TricksWon));

            for (int i = 1; i < candidates.Count; i++)
            {
                var move = candidates[i];
                var score = CalculateScore(move, handsLeft);

                int currentPlayerScore = score.TricksWon[_state.CurrentPlayerIndex];
                int bestPlayerScore = bestScore.TricksWon[_state.CurrentPlayerIndex];

                if (currentPlayerScore > bestPlayerScore)
                {
                    // Found a better move - clear the list and start fresh
                    bestMoves.Clear();
                    bestMoves.Add(move.WithExpectedTricks(score.TricksWon));
                    bestScore = score;
                }
                else if (currentPlayerScore == bestPlayerScore)
                {
                    // Found an equally good move - add to the list
                    bestMoves.Add(move.WithExpectedTricks(score.TricksWon));
                }
            }

            // Extend the list with equivalent smaller cards in each suit
            return ExtendCandidatesWithEquivalentMoves(bestMoves, bestScore);
        }

        /// <summary>
        /// Extends the list of best candidates by adding all smaller cards in the same suit
        /// until hitting an opponent's card. These cards are strategically equivalent since
        /// they're consecutive cards owned by the current player.
        /// </summary>
        /// <param name="bestMoves">List of optimal candidate moves (largest cards in each suit)</param>
        /// <param name="bestScore">The optimal score achieved by these moves</param>
        /// <returns>Extended list including all equivalent smaller cards</returns>
        private List<PerfectCardMove> ExtendCandidatesWithEquivalentMoves(List<PerfectCardMove> bestMoves, Score2 bestScore)
        {
            var extendedMoves = new List<PerfectCardMove>();
            int currentPlayer = _state.CurrentPlayerIndex;

            foreach (var candidate in bestMoves)
            {
                // Add the candidate itself
                extendedMoves.Add(candidate);

                var suitMoves = _state.Moves[(int)candidate.Card.Suit];
                int candidateIdx = candidate.ListIndex;

                // Search for smaller cards (higher indices) until we hit an opponent's card
                for (int i = candidateIdx + 1; i < suitMoves.Count; i++)
                {
                    var move = suitMoves[i];

                    if (!move.Available)
                    {
                        continue; // Skip unavailable cards
                    }

                    if (move.PlayerIndex != currentPlayer)
                    {
                        // Hit an opponent's card - stop extending
                        break;
                    }

                    // Found a smaller card owned by current player - add it with the same score
                    extendedMoves.Add(move.WithExpectedTricks(bestScore.TricksWon));
                }
            }

            return extendedMoves;
        }

        /// <summary>
        /// Calculates the best follow move for the given player responding to a lead card.
        /// Optimized to avoid enumerating all candidates.
        /// Returns a PerfectCardMove with ExpectedTricks populated.
        /// </summary>
        public PerfectCardMove BestFollowCard(PerfectCardMove leadMove)
        {
            // Validate that the current player is different from the lead player
            if (_state.CurrentPlayerIndex == leadMove.PlayerIndex)
            {
                throw new InvalidOperationException(
                    $"Cannot find follow move: current player ({_state.CurrentPlayerIndex}) is the same as lead player ({leadMove.PlayerIndex}). " +
                    "The game state must be advanced before calling BestFollowMove.");
            }

            int currentPlayer = _state.CurrentPlayerIndex;
            var suitMoves = _state.Moves[(int)leadMove.Card.Suit];

            // First, try to find a card in the lead suit
            var followMove = FindBestFollowInSuit(leadMove, suitMoves, currentPlayer);
            
            if (followMove != null)
            {
                // Can follow suit - calculate expected score for this move
                int handsLeft = CountRemainingHands();
                Score2 expectedScore = CalculateFollowScore(leadMove, followMove, handsLeft);
                return followMove.WithExpectedTricks(expectedScore.TricksWon);
            }

            // Cannot follow suit - check if we must play trump
            if (leadMove.Card.Suit != _trumpSuit)
            {
                // Lead suit is not trump - must play trump if available
                var trumpMoves = _state.Moves[(int)_trumpSuit];
                var trumpMove = FindSmallestCardInSuit(trumpMoves, currentPlayer);
                
                if (trumpMove != null)
                {
                    // Must play smallest trump
                    int handsLeft = CountRemainingHands();
                    Score2 expectedScore = CalculateFollowScore(leadMove, trumpMove, handsLeft);
                    return trumpMove.WithExpectedTricks(expectedScore.TricksWon);
                }
            }

            // Cannot follow suit and no trump available - evaluate all discard candidates and pick the best
            var discardCandidates = FindBestDiscard(leadMove.Card.Suit, currentPlayer);
            return EvaluateBestDiscard(leadMove, discardCandidates);
        }

        /// <summary>
        /// Evaluates multiple discard candidates and returns the one with the best expected score.
        /// </summary>
        private PerfectCardMove EvaluateBestDiscard(PerfectCardMove leadMove, List<PerfectCardMove> discardCandidates)
        {
            int handsLeft = CountRemainingHands();
            int currentPlayer = _state.CurrentPlayerIndex;

            PerfectCardMove bestDiscard = discardCandidates[0];
            Score2 bestScore = CalculateFollowScore(leadMove, discardCandidates[0], handsLeft);

            for (int i = 1; i < discardCandidates.Count; i++)
            {
                var candidate = discardCandidates[i];
                var score = CalculateFollowScore(leadMove, candidate, handsLeft);

                // Choose the discard that maximizes current player's tricks
                if (score.TricksWon[currentPlayer] > bestScore.TricksWon[currentPlayer])
                {
                    bestDiscard = candidate;
                    bestScore = score;
                }
            }

            return bestDiscard.WithExpectedTricks(bestScore.TricksWon);
        }

        /// <summary>
        /// Calculates the expected score after playing a lead and follow card.
        /// This simulates the trick completion and subsequent optimal play.
        /// </summary>
        private Score2 CalculateFollowScore(PerfectCardMove leadMove, PerfectCardMove followMove, int handsLeft)
        {
            // Mark both cards as played
            _state.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = false;
            _state.Moves[(int)followMove.Card.Suit][followMove.ListIndex].Available = false;

            // Determine winner of this trick
            int winnerIndex = GetWinner(leadMove, followMove);

            // Save current player
            int originalPlayer = _state.CurrentPlayerIndex;

            // Set winner as current player for next trick
            _state.CurrentPlayerIndex = winnerIndex;

            // Recursively calculate score for remaining hands
            Score2 futureScore = CalculateScoreCore(handsLeft - 1);

            // Update result: increment trick count for winner
            Score2 result = futureScore.IncrementPlayer(winnerIndex);

            // Restore state
            _state.Moves[(int)leadMove.Card.Suit][leadMove.ListIndex].Available = true;
            _state.Moves[(int)followMove.Card.Suit][followMove.ListIndex].Available = true;
            _state.CurrentPlayerIndex = originalPlayer;

            return result;
        }

        /// <summary>
        /// Finds the best card to play in the lead suit.
        /// Cards are sorted from highest to lowest (A, K, Q, J, 10, 9, 8, 7).
        /// Strategy: Try to win with the smallest winning card, or lose with the smallest losing card.
        /// </summary>
        private PerfectCardMove? FindBestFollowInSuit(PerfectCardMove leadMove, List<PerfectCardMove> suitMoves, int currentPlayer)
        {
            int startIndex = leadMove.ListIndex;
            
            // First, look for a card that can WIN (larger than lead card)
            // Search left from the lead card (towards index 0 = higher cards)
            for (int i = startIndex - 1; i >= 0; i--)
            {
                var move = suitMoves[i];
                if (move.Available && move.PlayerIndex == currentPlayer)
                {
                    return move; // Found smallest winning card
                }
            }

            // No winning card found - need to LOSE with smallest card
            // Search from the absolute end of the list (highest index = smallest cards)
            // We search the entire remaining portion because we want the absolute smallest card
            for (int i = suitMoves.Count - 1; i > startIndex; i--)
            {
                var move = suitMoves[i];
                if (move.Available && move.PlayerIndex == currentPlayer)
                {
                    return move; // Return smallest losing card
                }
            }

            // No cards found in the range after startIndex
            // This shouldn't happen if player has cards in this suit, but check just in case
            return null;
        }

        /// <summary>
        /// Calculates the maximum number of tricks the current player can win in a suit
        /// if the opponent always leads with their strongest available cards.
        /// 
        /// Algorithm:
        /// - Cards are sorted from highest to lowest (A, K, Q, J, 10, 9, 8, 7)
        /// - Start from the highest card (left/beginning of the list - index 0)
        /// - If it's current player's card, count +1 and move right
        /// - If it's opponent's card, opponent leads with it
        ///   - Current player must follow with their lowest card (right end of their cards)
        ///   - Remove both cards
        /// - If current player has no cards to follow, remaining opponent cards are ignored
        /// 
        /// Examples (highest to lowest):
        /// - ABBA -> 1 (B leads with highest B, A discards lowest A, remaining highest A wins)
        /// - AABB -> 2 (both A's win)
        /// - BBAA -> 0 (B's eat both A's)
        /// - K,10 vs A,Q (BABA) -> 1 (B leads A, A discards 10, K remains and wins)
        /// </summary>
        private int CalculateMaxTricksIfOpponentLeads(List<PerfectCardMove> suitMoves, int currentPlayer)
        {
            int otherPlayer = (currentPlayer + 1) % 2;
            int value = 0;
            
            // Track which cards are still in play
            var inPlay = new bool[suitMoves.Count];
            int availableCount = 0;
            
            for (int i = 0; i < suitMoves.Count; i++)
            {
                if (suitMoves[i].Available)
                {
                    inPlay[i] = true;
                    availableCount++;
                }
            }

            if (availableCount == 0)
            {
                return 0;
            }

            while (true)
            {
                // Find highest card still in play (cards sorted highest to lowest, so search from beginning)
                int highestIdx = -1;
                for (int i = 0; i < suitMoves.Count; i++)
                {
                    if (inPlay[i])
                    {
                        highestIdx = i;
                        break;
                    }
                }

                if (highestIdx == -1)
                {
                    break; // No more cards
                }

                if (suitMoves[highestIdx].PlayerIndex == currentPlayer)
                {
                    // Current player has highest card - wins this trick
                    value++;
                    inPlay[highestIdx] = false;

                    // Opponent must follow with their lowest card (if any)
                    int lowestOpponentIdx = FindLowestCardIndexInSuit(suitMoves, otherPlayer, inPlay, highestIdx);
                    if (lowestOpponentIdx != -1)
                    {
                        inPlay[lowestOpponentIdx] = false;
                    }
                }
                else
                {
                    // Opponent has highest card - opponent leads with it
                    // Current player must follow with their LOWEST card
                    int lowestCurrentPlayerIdx = FindLowestCardIndexInSuit(suitMoves, currentPlayer, inPlay, highestIdx);

                    if (lowestCurrentPlayerIdx == -1)
                    {
                        // Current player has no cards left in this suit
                        // Remaining opponent cards don't matter
                        break;
                    }

                    // Remove both cards (opponent's highest and current player's lowest)
                    inPlay[highestIdx] = false;
                    inPlay[lowestCurrentPlayerIdx] = false;
                }
            }

            return value;
        }

        /// <summary>
        /// Finds the best card to discard when unable to follow suit.
        /// Returns a list of candidate discards for evaluation.
        /// - Returns 1 card for safe discards or hopeless suits
        /// - Returns multiple cards (up to 3) for unsafe non-hopeless situations
        /// </summary>
        private List<PerfectCardMove> FindBestDiscard(Suit leadSuit, int currentPlayer)
        {
            var suitEvaluations = new List<(int suitIndex, PerfectCardMove smallestCard, int valueWith, int valueWithout, int currentPlayerCount, int opponentCount)>();

            int otherPlayer = (currentPlayer + 1) % 2;

            // Evaluate each suit (except the lead suit and trump suit)
            for (int suitIndex = 0; suitIndex < _state.Moves.Count; suitIndex++)
            {
                if (suitIndex == (int)leadSuit || suitIndex == (int)_trumpSuit)
                    continue;

                var suitMoves = _state.Moves[suitIndex];
                var smallestCard = FindSmallestCardInSuit(suitMoves, currentPlayer);

                if (smallestCard == null)
                    continue;

                // Calculate value with the smallest card
                int valueWith = CalculateMaxTricksIfOpponentLeads(suitMoves, currentPlayer);

                // Temporarily mark smallest card as unavailable
                suitMoves[smallestCard.ListIndex].Available = false;

                // Calculate value without the smallest card
                int valueWithout = CalculateMaxTricksIfOpponentLeads(suitMoves, currentPlayer);

                // Restore the card
                suitMoves[smallestCard.ListIndex].Available = true;

                // Count cards for each player in this suit
                int currentPlayerCount = 0;
                int opponentCount = 0;

                foreach (var move in suitMoves)
                {
                    if (move.Available)
                    {
                        if (move.PlayerIndex == currentPlayer)
                            currentPlayerCount++;
                        else if (move.PlayerIndex == otherPlayer)
                            opponentCount++;
                    }
                }

                suitEvaluations.Add((suitIndex, smallestCard, valueWith, valueWithout, currentPlayerCount, opponentCount));
            }

            if (!suitEvaluations.Any())
            {
                throw new InvalidOperationException("No available cards to discard");
            }

            // First, look for safe discards (valueWith == valueWithout)
            var safeDiscards = suitEvaluations.Where(e => e.valueWith == e.valueWithout).ToList();
            if (safeDiscards.Any())
            {
                // Safe discard found - return just the first one (no need to evaluate further)
                return new List<PerfectCardMove> { safeDiscards[0].smallestCard };
            }

            // No safe discards - use strategic analysis to get candidates
            return FindBestUnsafeDiscards(suitEvaluations, currentPlayer);
        }

        /// <summary>
        /// Finds the best unsafe discards when no safe options exist.
        /// Returns a list of candidates for evaluation:
        /// - 1 card for hopeless suits (already dead, clearly the best bad option)
        /// - Multiple cards (up to 3) for non-hopeless situations (need scoring evaluation)
        /// 
        /// Prioritizes:
        /// 0. Hopeless suits (valueWith = 0) - return one card from longest hopeless suit
        /// 1. Non-hopeless: return multiple candidates with heuristic best first
        /// </summary>
        private List<PerfectCardMove> FindBestUnsafeDiscards(
            List<(int suitIndex, PerfectCardMove smallestCard, int valueWith, int valueWithout, int currentPlayerCount, int opponentCount)> evaluations,
            int currentPlayer)
        {
            // Priority 0: Hopeless suits (valueWith = 0)
            var hopelessSuits = evaluations.Where(e => e.valueWith == 0).ToList();
            if (hopelessSuits.Any())
            {
                // Discard from the longest hopeless suit - return only one card (clearly best)
                var hopelessCard = hopelessSuits.OrderByDescending(e => e.currentPlayerCount).First().smallestCard;
                return new List<PerfectCardMove> { hopelessCard };
            }

            // Non-hopeless unsafe discards - return multiple candidates for evaluation
            var candidates = new List<PerfectCardMove>();

            // Get the heuristic best choice first
            var heuristicBest = GetHeuristicBestDiscard(evaluations);
            candidates.Add(heuristicBest);

            // Add other smallest cards from other suits as alternatives
            foreach (var eval in evaluations)
            {
                // Skip the heuristic best (already added)
                if (eval.smallestCard.Card.Suit == heuristicBest.Card.Suit)
                    continue;

                candidates.Add(eval.smallestCard);
            }

            return candidates;
        }

        /// <summary>
        /// Uses heuristics to pick the best unsafe discard.
        /// This is the original heuristic logic, now used to order candidates.
        /// </summary>
        private PerfectCardMove GetHeuristicBestDiscard(
            List<(int suitIndex, PerfectCardMove smallestCard, int valueWith, int valueWithout, int currentPlayerCount, int opponentCount)> evaluations)
        {
            // Priority 1: Discard from suits where opponent is VOID
            // (These are "orphaned" winners we may never get to play if we don't get the lead)
            var opponentVoidSuits = evaluations.Where(e => e.opponentCount == 0).ToList();
            if (opponentVoidSuits.Any())
            {
                // Among void suits, prefer discarding from longer ones
                return opponentVoidSuits.OrderByDescending(e => e.currentPlayerCount).First().smallestCard;
            }

            // Priority 2: All remaining suits have opponent cards (guaranteed future interactions)
            // Prefer discarding from longer suits (more flexibility for future discards)
            var longestSuit = evaluations.OrderByDescending(e => e.currentPlayerCount).First();
            
            // Check if multiple suits have the same length
            var sameLengthSuits = evaluations.Where(e => e.currentPlayerCount == longestSuit.currentPlayerCount).ToList();
            
            if (sameLengthSuits.Count == 1)
            {
                return longestSuit.smallestCard;
            }

            // Priority 3: Final tie-breaker - minimize valueWithout
            return sameLengthSuits.OrderBy(e => e.valueWithout).First().smallestCard;
        }

        /// <summary>
        /// Finds the smallest available card for current player in a suit.
        /// Since suitMoves is sorted from highest to lowest rank, we search from the end.
        /// </summary>
        private PerfectCardMove? FindSmallestCardInSuit(List<PerfectCardMove> suitMoves, int currentPlayer)
        {
            // Iterate from the end since list is sorted highest to lowest
            for (int i = suitMoves.Count - 1; i >= 0; i--)
            {
                var move = suitMoves[i];
                if (move.Available && move.PlayerIndex == currentPlayer)
                {
                    return move;
                }
            }
            return null;
        }

        /// <summary>
        /// Internal helper method that returns list of best follow moves.
        /// Does not attach ExpectedTricks (to avoid recursion overhead).
        /// </summary>
        private List<PerfectCardMove> BestFollowMove(PerfectCardMove leadMove)
        {
            int currentPlayer = _state.CurrentPlayerIndex;
            var suitMoves = _state.Moves[(int)leadMove.Card.Suit];

            // First, try to find cards in the lead suit
            var followMoves = new List<PerfectCardMove>();
            
            foreach (var move in suitMoves)
            {
                if (move.Available && move.PlayerIndex == currentPlayer)
                {
                    followMoves.Add(move);
                }
            }

            if (followMoves.Any())
            {
                // Can follow suit - return all cards in the suit
                // Note: Could optimize by using FindBestFollowInSuit logic, but returning all
                // gives more options for exhaustive search when _exploreAllOptions is true
                return followMoves;
            }

            // Cannot follow suit - check if must play trump
            if (leadMove.Card.Suit != _trumpSuit)
            {
                var trumpMoves = _state.Moves[(int)_trumpSuit];
                var trumpMove = FindSmallestCardInSuit(trumpMoves, currentPlayer);
                
                if (trumpMove != null)
                {
                    // Must play smallest trump - return as single-item list
                    return new List<PerfectCardMove> { trumpMove };
                }
            }

            // Cannot follow suit and no trump available - return discard candidates
            return FindBestDiscard(leadMove.Card.Suit, currentPlayer);
        }

        /// <summary>
        /// Evaluates a specific lead move and returns the resulting score.
        /// Always maximizes the score for the current player.
        /// Called after a hand is completed.
        /// </summary>
        public Score2 CalculateScore(PerfectCardMove move, int handsLeft)
        {
            // Base case: no more hands to play
            if (handsLeft <= 0)
            {
                return new Score2(0, 0);
            }

            int leadPlayer = _state.CurrentPlayerIndex;

            // Mark the lead card as played
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = false;

            // Get next player
            int nextPlayer = GetNextPlayer(_state);
            _state.CurrentPlayerIndex = nextPlayer;

            // Get follow move options from next player
            var followMoves = BestFollowMove(move);

            // Determine which follow moves to evaluate
            var movesToEvaluate = _exploreAllOptions 
                ? followMoves 
                : new List<PerfectCardMove> { followMoves[0] };

            // Evaluate follow options and get best score
            Score2 result = EvaluateAllFollowOptions(move, movesToEvaluate, handsLeft);

            // Restore state: mark lead move as available again
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = true;

            // Restore current player
            _state.CurrentPlayerIndex = leadPlayer;

            return result;
        }

        /// <summary>
        /// Evaluates all possible follow moves and returns the best score for the follower.
        /// Used in endgame scenarios where exhaustive search is feasible.
        /// </summary>
        private Score2 EvaluateAllFollowOptions(PerfectCardMove leadMove, List<PerfectCardMove> followMoves, int handsLeft)
        {
            int followerPlayer = _state.CurrentPlayerIndex;
            Score2 bestScore = new Score2(0, 0);
            bool firstIteration = true;

            foreach (var followMove in followMoves)
            {
                // Mark the follow card as played
                _state.Moves[(int)followMove.Card.Suit][followMove.ListIndex].Available = false;

                // Determine winner of this trick
                int winnerIndex = GetWinner(leadMove, followMove);

                // Set current player to winner
                _state.CurrentPlayerIndex = winnerIndex;

                // Recursively calculate score for remaining hands
                Score2 futureScore = CalculateScoreCore(handsLeft - 1);

                // Update result: increment trick count for winner
                Score2 currentScore = futureScore.IncrementPlayer(winnerIndex);

                // Restore the follow card
                _state.Moves[(int)followMove.Card.Suit][followMove.ListIndex].Available = true;

                // Follower picks the move that maximizes their own score
                if (firstIteration || currentScore.TricksWon[followerPlayer] > bestScore.TricksWon[followerPlayer])
                {
                    bestScore = currentScore;
                    firstIteration = false;
                }
            }

            // Restore current player (will be restored again in CalculateScore, but being safe)
            _state.CurrentPlayerIndex = followerPlayer;

            return bestScore;
        }

        /// <summary>
        /// Core recursive calculation - finds best lead move for current player
        /// </summary>
        private Score2 CalculateScoreCore(int handsLeft)
        {
            // Base case: no more hands to play
            if (handsLeft <= 0)
            {
                return new Score2(0, 0);
            }

            int currentPlayer = _state.CurrentPlayerIndex;
            var candidates = _state.GetCandidateMovesForCurrentPlayer().ToList();

            if (!candidates.Any())
            {
                // No moves available - game over
                return new Score2(0, 0);
            }

            // Find the move that maximizes the current player's score
            Score2 bestScore = CalculateScore(candidates[0], handsLeft);

            for (int i = 1; i < candidates.Count; i++)
            {
                var score = CalculateScore(candidates[i], handsLeft);
                
                if (score.TricksWon[currentPlayer] > bestScore.TricksWon[currentPlayer])
                {
                    bestScore = score;
                }
            }

            return bestScore;
        }

        /// <summary>
        /// Gets the next player index (for 2-player game)
        /// </summary>
        private int GetNextPlayer(PerfPerfectGameState state)
        {
            return (state.CurrentPlayerIndex + 1) % 2;
        }

        /// <summary>
        /// Determines the winner between two moves in Trump game.
        /// Trump cards beat non-trump cards. Highest trump wins if multiple trumps.
        /// If no trumps, highest card in lead suit wins.
        /// </summary>
        private int GetWinner(PerfectCardMove leadMove, PerfectCardMove followMove)
        {
            bool leadIsTrump = leadMove.Card.Suit == _trumpSuit;
            bool followIsTrump = followMove.Card.Suit == _trumpSuit;

            // Both cards are trump - highest rank wins
            if (leadIsTrump && followIsTrump)
            {
                return leadMove.Card.Rank > followMove.Card.Rank 
                    ? leadMove.PlayerIndex 
                    : followMove.PlayerIndex;
            }

            // Follow card is trump, lead is not - follow wins
            if (followIsTrump && !leadIsTrump)
            {
                return followMove.PlayerIndex;
            }

            // Lead card is trump, follow is not - lead wins
            if (leadIsTrump && !followIsTrump)
            {
                return leadMove.PlayerIndex;
            }

            // Neither is trump - must follow suit
            if (followMove.Card.Suit != leadMove.Card.Suit)
            {
                // Follower couldn't follow suit - leader wins
                return leadMove.PlayerIndex;
            }

            // Same suit (non-trump) - highest rank wins
            return leadMove.Card.Rank > followMove.Card.Rank 
                ? leadMove.PlayerIndex 
                : followMove.PlayerIndex;
        }

        /// <summary>
        /// Counts remaining hands based on available cards for current player
        /// </summary>
        private int CountRemainingHands()
        {
            int availableCards = _state.GetAvailableMovesForPlayer(_state.CurrentPlayerIndex).Count();
            return availableCards;
        }

        /// <summary>
        /// Plays a move and updates the game state permanently
        /// </summary>
        public void PlayMove(PerfectCardMove move)
        {
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = false;
            _state.AdvanceTurn();
        }

        /// <summary>
        /// Finds the index of the lowest card in the suit for the specified player.
        /// Searches from the end of the list since cards are sorted highest to lowest.
        /// </summary>
        /// <param name="suitMoves">List of moves in the suit (sorted highest to lowest)</param>
        /// <param name="playerIndex">The player to find cards for</param>
        /// <param name="inPlay">Boolean array indicating which cards are still available</param>
        /// <param name="highestCardIdx">Index of the highest card currently being processed (search boundary)</param>
        /// <returns>Index of the lowest card, or -1 if player has no cards left</returns>
        private int FindLowestCardIndexInSuit(List<PerfectCardMove> suitMoves, int playerIndex, bool[] inPlay, int highestCardIdx)
        {
            // Search from end (lowest cards) towards the highest card index
            // Only search cards lower than highestCardIdx (i > highestCardIdx)
            for (int i = suitMoves.Count - 1; i > highestCardIdx; i--)
            {
                if (inPlay[i] && suitMoves[i].PlayerIndex == playerIndex)
                {
                    return i;
                }
            }
            return -1; // Player has no cards left in this suit
        }
    }
}
