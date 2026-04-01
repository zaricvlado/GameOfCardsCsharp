using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Represents the score in a 2-player Sans game
    /// </summary>
    public readonly struct Score2
    {
        /// <summary>
        /// Number of tricks won by each player [Player0, Player1]
        /// </summary>
        public int[] TricksWon { get; }

        public Score2(int player0Tricks, int player1Tricks)
        {
            TricksWon = new[] { player0Tricks, player1Tricks };
        }

        public Score2(int[] tricksWon)
        {
            if (tricksWon == null || tricksWon.Length != 2)
            {
                throw new ArgumentException("Must provide exactly 2 values", nameof(tricksWon));
            }
            TricksWon = new int[2];
            Array.Copy(tricksWon, TricksWon, 2);
        }

        /// <summary>
        /// Adds trick results to current score
        /// </summary>
        public Score2 Add(Score2 other)
        {
            return new Score2(
                TricksWon[0] + other.TricksWon[0],
                TricksWon[1] + other.TricksWon[1]
            );
        }

        /// <summary>
        /// Increments the trick count for a specific player
        /// </summary>
        public Score2 IncrementPlayer(int playerIndex)
        {
            var newTricks = new int[2];
            Array.Copy(TricksWon, newTricks, 2);
            newTricks[playerIndex]++;
            return new Score2(newTricks);
        }

        public override string ToString()
        {
            return $"P0:{TricksWon[0]} P1:{TricksWon[1]}";
        }
    }

    /// <summary>
    /// Perfect information game for 2-player Sans (no-trump).
    /// Uses recursive evaluation to determine optimal moves.
    /// </summary>
    public class Sans2PerfectGame
    {
        private readonly PerfPerfectGameState _state;

        public PerfPerfectGameState State => _state;

        public Sans2PerfectGame(PerfPerfectGameState state)
        {
            if (state.Players.Count != 2)
            {
                throw new ArgumentException("SansPerfectGame only supports 2 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Sans)
            {
                throw new ArgumentException("SansPerfectGame requires Sans game mode", nameof(state));
            }

            _state = state;
        }

        /// <summary>
        /// Calculates the best lead card for the current player in a 2-player game.
        /// Returns a PerfectCardMove with ExpectedTricks populated.
        /// </summary>
        public PerfectCardMove BestLeadCard()
        {
            int handsLeft = CountRemainingHands();
            var candidates = _state.GetCandidateMovesForCurrentPlayer().ToList();

            if (!candidates.Any())
            {
                throw new InvalidOperationException("No available moves for current player");
            }

            PerfectCardMove bestMove = candidates[0];
            Score2 bestScore = CalculateScore(candidates[0], handsLeft);

            for (int i = 1; i < candidates.Count; i++)
            {
                var move = candidates[i];
                var score = CalculateScore(move, handsLeft);

                // Choose move that maximizes current player's tricks
                if (score.TricksWon[_state.CurrentPlayerIndex] > bestScore.TricksWon[_state.CurrentPlayerIndex])
                {
                    bestMove = move;
                    bestScore = score;
                }
            }

            // Return the best move WITH expected tricks attached
            return bestMove.WithExpectedTricks(bestScore.TricksWon);
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
            
            if (followMove == null)
            {
                // Cannot follow suit - find a card to discard
                followMove = FindBestDiscard(leadMove.Card.Suit, currentPlayer);
            }

            // Calculate expected score for this follow move
            // We need to simulate playing both the lead card and follow card
            int handsLeft = CountRemainingHands();
            Score2 expectedScore = CalculateFollowScore(leadMove, followMove, handsLeft);

            // Return the follow move WITH expected tricks attached
            return followMove.WithExpectedTricks(expectedScore.TricksWon);
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
                }
                else
                {
                    // Opponent has highest card - opponent leads with it
                    // Current player must follow with their LOWEST card (search from end)
                    int lowestCurrentPlayerIdx = -1;
                    for (int i = suitMoves.Count - 1; i >= 0; i--)
                    {
                        if (inPlay[i] && suitMoves[i].PlayerIndex == currentPlayer)
                        {
                            lowestCurrentPlayerIdx = i;
                            break;
                        }
                    }

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
        /// Uses strategic evaluation: discard from suits where removing the smallest card
        /// doesn't reduce trick-winning potential, or uses sophisticated analysis for unsafe discards.
        /// </summary>
        private PerfectCardMove FindBestDiscard(Suit leadSuit, int currentPlayer)
        {
            var suitEvaluations = new List<(int suitIndex, PerfectCardMove smallestCard, int valueWith, int valueWithout, int currentPlayerCount, int opponentCount)>();

            int otherPlayer = (currentPlayer + 1) % 2;

            // Evaluate each suit (except the lead suit)
            for (int suitIndex = 0; suitIndex < _state.Moves.Count; suitIndex++)
            {
                if (suitIndex == (int)leadSuit)
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
                // Safe discard found - the smallest card is worthless
                return safeDiscards[0].smallestCard;
            }

            // No safe discards - use strategic analysis
            return FindBestUnsafeDiscard(suitEvaluations, currentPlayer);
        }

        /// <summary>
        /// Finds the best discard when all options would lose exactly 1 trick.
        /// Prioritizes:
        /// 0. Hopeless suits (valueWith = 0) - longest one first (already dead)
        /// 1. Discard from suits where opponent is VOID (orphaned winners we may never play)
        /// 2. Prefer longer suits (more flexibility for future discards)
        /// 3. Minimize valueWithout as final tie-breaker (since all losses are equal)
        /// </summary>
        private PerfectCardMove FindBestUnsafeDiscard(
            List<(int suitIndex, PerfectCardMove smallestCard, int valueWith, int valueWithout, int currentPlayerCount, int opponentCount)> evaluations,
            int currentPlayer)
        {
            // Priority 0: Hopeless suits (valueWith = 0) - longest first
            var hopelessSuits = evaluations.Where(e => e.valueWith == 0).ToList();
            if (hopelessSuits.Any())
            {
                // Discard from the longest hopeless suit (most dead weight)
                return hopelessSuits.OrderByDescending(e => e.currentPlayerCount).First().smallestCard;
            }

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
            // (Since all losses are equal at 1, this is least important)
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

            // Get best follow move from next player
            PerfectCardMove move2 = BestFollowMove(move);

            // Mark the follow card as played
            _state.Moves[(int)move2.Card.Suit][move2.ListIndex].Available = false;

            // Determine winner of this trick
            int winnerIndex = GetWinner(move, move2);

            // Set current player to winner
            _state.CurrentPlayerIndex = winnerIndex;

            // Recursively calculate score for remaining hands
            // (Winner leads next trick, so this maximizes winner's score)
            Score2 futureScore = CalculateScoreCore(handsLeft - 1);

            // Update result: increment trick count for winner
            Score2 result = futureScore.IncrementPlayer(winnerIndex);

            // Restore state: mark moves as available again
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = true;
            _state.Moves[(int)move2.Card.Suit][move2.ListIndex].Available = true;

            // Restore current player
            _state.CurrentPlayerIndex = leadPlayer;

            return result;
        }

        /// <summary>
        /// Internal helper method that doesn't attach ExpectedTricks (to avoid recursion overhead)
        /// </summary>
        private PerfectCardMove BestFollowMove(PerfectCardMove leadMove)
        {
            int currentPlayer = _state.CurrentPlayerIndex;
            var suitMoves = _state.Moves[(int)leadMove.Card.Suit];

            // First, try to find a card in the lead suit
            var followMove = FindBestFollowInSuit(leadMove, suitMoves, currentPlayer);
            
            if (followMove != null)
            {
                return followMove;
            }

            // Cannot follow suit - find a card to discard
            return FindBestDiscard(leadMove.Card.Suit, currentPlayer);
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
        /// Determines the winner between two moves in Sans (no-trump).
        /// Must follow suit; highest rank in lead suit wins.
        /// </summary>
        private int GetWinner(PerfectCardMove leadMove, PerfectCardMove followMove)
        {
            // In Sans (no trump), must follow suit
            if (followMove.Card.Suit != leadMove.Card.Suit)
            {
                // Follower couldn't follow suit - leader wins
                return leadMove.PlayerIndex;
            }

            // Same suit - highest rank wins
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
    }
}
