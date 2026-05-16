using System;
using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Sans
{
    /// <summary>
    /// Perfect information game for 3-player Sans (no-trump).
    /// One player is the declarer, the other two are defenders cooperating
    /// against the declarer. Uses recursive minimax-style evaluation with
    /// short candidate lists for a fast first cut. Returns <see cref="Score3"/>.
    /// </summary>
    public class SansPerfectGame
    {
        private readonly PerfPerfectGameState _state;

        public PerfPerfectGameState State => _state;

        public SansPerfectGame(PerfPerfectGameState state)
        {
            if (state.Players.Count != 3)
            {
                throw new ArgumentException(
                    "SansPerfectGame only supports 3 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Sans)
            {
                throw new ArgumentException(
                    "SansPerfectGame requires Sans game mode", nameof(state));
            }

            _state = state;
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Returns the lead candidates that yield the maximum coalition score
        /// for the current player. Uses the declarer or defender short-list
        /// depending on whether the current player is the declarer.
        /// </summary>
        public List<PerfectCardMove> BestLeadCards()
        {
            int leader = _state.CurrentPlayerIndex;

            var candidates = GetShortCandidates(leader);
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No lead candidates available for player {leader}");
            }

            var bestMoves = new List<PerfectCardMove>();
            Score3 bestScore = default;
            bool first = true;

            foreach (var candidate in candidates)
            {
                var score = CalculateScore(candidate, null);

                int candidateLeaderScore = LeaderScore(score, leader);
                int currentBestLeaderScore = LeaderScore(bestScore, leader);

                if (first || candidateLeaderScore > currentBestLeaderScore)
                {
                    bestMoves.Clear();
                    bestMoves.Add(candidate.WithExpectedTricks(score.IndividualTricks));
                    bestScore = score;
                    first = false;
                }
                else if (candidateLeaderScore == currentBestLeaderScore)
                {
                    bestMoves.Add(candidate.WithExpectedTricks(score.IndividualTricks));
                }
            }

            return bestMoves;
        }

        /// <summary>
        /// Returns the best follow card(s) for the current player.
        /// - <paramref name="move2"/> == null: current player is 2nd to play.
        /// - <paramref name="move2"/> != null: current player is 3rd to play.
        ///
        /// By contract, <paramref name="move1"/> (and <paramref name="move2"/>
        /// if provided) must already be marked <c>Available = false</c> on the
        /// underlying state before calling.
        /// </summary>
        public List<PerfectCardMove> BestFollowCards(
            PerfectCardMove move1,
            PerfectCardMove? move2 = null)
        {
            int player = _state.CurrentPlayerIndex;
            Suit leadSuit = move1.Card.Suit;
            var ownInSuit = _state.GetAvailableMovesInSuit(player, leadSuit);

            PerfectCardMove chosen = move2 == null
                ? PickSecondSeatFollow(move1, player, leadSuit, ownInSuit)
                : PickThirdSeatFollow(move1, move2, player, leadSuit, ownInSuit);

            return new List<PerfectCardMove> { chosen };
        }

        /// <summary>
        /// Discard heuristic for the declarer when unable to follow suit.
        /// Returns all legal discard candidates (one per non-led suit where the
        /// declarer holds cards), sorted best-first by
        /// <see cref="SuitDefenseOutcome.DiscardPriority"/> and then by longest
        /// suit. The attacker is modeled as the next seat clockwise (a defender).
        /// </summary>
        public List<PerfectCardMove> BestDeclarerDiscard(int playerIndex, Suit leadSuit)
            => RankDiscards(playerIndex, leadSuit, attackerIndex: NextPlayer(playerIndex));

        /// <summary>
        /// Discard heuristic for a defender when unable to follow suit.
        /// Returns all legal discard candidates (one per non-led suit where the
        /// defender holds cards), sorted best-first by
        /// <see cref="SuitDefenseOutcome.DiscardPriority"/> and then by longest
        /// suit. The attacker is the declarer.
        /// </summary>
        public List<PerfectCardMove> BestDefenderDiscard(int playerIndex, Suit leadSuit)
            => RankDiscards(playerIndex, leadSuit, attackerIndex: _state.DeclarerIndex);

        /// <summary>
        /// Shared discard ranking used by both declarer and defender policies.
        /// For each non-led suit where the holder has at least one card, asks
        /// <see cref="PerfPerfectGameState.AnalyzeSuitDiscard"/> for the cost of
        /// discarding the holder's lowest card in that suit, then sorts by
        /// <see cref="SuitDefenseOutcome.DiscardPriority"/> ascending and by
        /// <see cref="SuitDiscardAnalysis.DefenderLength"/> descending.
        /// </summary>
        private List<PerfectCardMove> RankDiscards(
            int holderIndex, Suit leadSuit, int attackerIndex)
        {
            // At most 3 non-led suits to consider.
            var ranked = new List<SuitDiscardAnalysis>(capacity: 3);

            for (int s = 0; s < _state.Moves.Count; s++)
            {
                if (s == (int)leadSuit)
                {
                    continue;
                }

                var analysis = _state.AnalyzeSuitDiscard(
                    (Suit)s, attackerIndex, holderIndex);

                if (analysis.Candidate != null)
                {
                    ranked.Add(analysis);
                }
            }

            if (ranked.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Player {holderIndex} has no cards to discard outside suit {leadSuit}.");
            }

            return ranked
                .OrderBy(a => a.DiscardCost.DiscardPriority)
                .ThenByDescending(a => a.DefenderLength)
                .Select(a => a.Candidate!)
                .ToList();
        }

        /// <summary>
        /// Calculates the resulting <see cref="Score3"/> after playing
        /// <paramref name="move1"/> as the lead and (optionally) a chosen
        /// <paramref name="move2"/> for the 2nd seat. The 3rd seat (and all
        /// future tricks) are filled in via <see cref="BestFollowCards"/> /
        /// recursive lookahead.
        /// </summary>
        public Score3 CalculateScore(PerfectCardMove move1, PerfectCardMove? move2)
        {
            int leader = _state.CurrentPlayerIndex;
            int handsLeft = CountRemainingHands();

            // Base case: nothing more to play.
            if (handsLeft <= 0)
            {
                return ZeroScore();
            }

            // Mark lead.
            var leadMove = _state.Moves[(int)move1.Card.Suit][move1.ListIndex];
            leadMove.Available = false;

            // 2nd seat
            int secondSeat = NextPlayer(leader);
            _state.CurrentPlayerIndex = secondSeat;
            PerfectCardMove m2 = move2 ?? BestFollowCards(move1, null)[0];
            var secondMove = _state.Moves[(int)m2.Card.Suit][m2.ListIndex];
            secondMove.Available = false;

            // 3rd seat
            int thirdSeat = NextPlayer(secondSeat);
            _state.CurrentPlayerIndex = thirdSeat;
            PerfectCardMove m3 = BestFollowCards(move1, m2)[0];
            var thirdMove = _state.Moves[(int)m3.Card.Suit][m3.ListIndex];
            thirdMove.Available = false;

            // Resolve trick.
            int winner = GetTrickWinner(move1, m2, m3);
            _state.CurrentPlayerIndex = winner;

            Score3 future = CalculateScoreCore(handsLeft - 1);
            Score3 result = future.IncrementPlayer(winner, _state.DeclarerIndex);

            // Restore.
            leadMove.Available = true;
            secondMove.Available = true;
            thirdMove.Available = true;
            _state.CurrentPlayerIndex = leader;

            return result;
        }

        // ==================== INTERNAL RECURSION ====================

        /// <summary>
        /// Recursive driver: for the current leader, picks the candidate that
        /// maximizes the coalition score from the short-list and returns the
        /// resulting score.
        /// </summary>
        private Score3 CalculateScoreCore(int handsLeft)
        {
            if (handsLeft <= 0)
            {
                return ZeroScore();
            }

            int leader = _state.CurrentPlayerIndex;
            var candidates = GetShortCandidates(leader);

            if (candidates.Count == 0)
            {
                return ZeroScore();
            }

            Score3 best = default;
            bool first = true;

            foreach (var candidate in candidates)
            {
                var score = CalculateScore(candidate, null);
                if (first || LeaderScore(score, leader) > LeaderScore(best, leader))
                {
                    best = score;
                    first = false;
                }
            }

            return best;
        }

        // ==================== FOLLOW LOGIC ====================

        private PerfectCardMove PickSecondSeatFollow(
            PerfectCardMove move1,
            int player,
            Suit leadSuit,
            IReadOnlyList<PerfectCardMove> ownInSuit)
        {
            if (IsDeclarer(player))
            {
                if (ownInSuit.Count == 0)
                {
                    return BestDeclarerDiscard(player, leadSuit)[0];
                }

                // Predict the 3rd seat's best card in suit; threshold is the
                // higher of move1's rank and that prediction.
                int thirdSeat = NextPlayer(player);
                var thirdHigh = _state.GetHighestAvailableMoveInSuit(thirdSeat, leadSuit);

                int threshold = (int)move1.Card.Rank;
                if (thirdHigh != null && (int)thirdHigh.Card.Rank > threshold)
                {
                    threshold = (int)thirdHigh.Card.Rank;
                }

                return FindCheapestStrongerInSuit(player, leadSuit, threshold)
                       ?? ownInSuit[ownInSuit.Count - 1]; // smallest in suit
            }

            // Defender in 2nd seat.
            if (ownInSuit.Count == 0)
            {
                return BestDefenderDiscard(player, leadSuit)[0];
            }

            bool firstSeatIsDeclarer = move1.PlayerIndex == _state.DeclarerIndex;

            if (firstSeatIsDeclarer)
            {
                // 2a1: declarer led, 3rd seat = other defender.
                var candidate = FindCheapestStrongerInSuit(
                    player, leadSuit, (int)move1.Card.Rank);

                if (candidate == null)
                {
                    return ownInSuit[ownInSuit.Count - 1];
                }

                int otherDef = OtherDefender(player);
                var otherHigh = _state.GetHighestAvailableMoveInSuit(otherDef, leadSuit);

                // If the other defender can beat the lead with a card cheaper
                // than ours, let them win — duck with smallest.
                if (otherHigh != null
                    && (int)otherHigh.Card.Rank > (int)move1.Card.Rank
                    && (int)otherHigh.Card.Rank < (int)candidate.Card.Rank)
                {
                    return ownInSuit[ownInSuit.Count - 1];
                }

                return candidate;
            }
            else
            {
                // 2a2: other defender led, declarer is in 3rd seat.
                var declHigh = _state.GetHighestAvailableMoveInSuit(
                    _state.DeclarerIndex, leadSuit);

                if (declHigh != null && (int)declHigh.Card.Rank > (int)move1.Card.Rank)
                {
                    // Declarer can beat partner — try to overtake.
                    return FindCheapestStrongerInSuit(player, leadSuit, (int)move1.Card.Rank)
                           ?? ownInSuit[ownInSuit.Count - 1];
                }

                // Partner already winning (or only declarer cards are smaller) — duck.
                return ownInSuit[ownInSuit.Count - 1];
            }
        }

        private PerfectCardMove PickThirdSeatFollow(
            PerfectCardMove move1,
            PerfectCardMove move2,
            int player,
            Suit leadSuit,
            IReadOnlyList<PerfectCardMove> ownInSuit)
        {
            PerfectCardMove currentlyWinning = CurrentlyWinning(move1, move2);

            if (IsDeclarer(player))
            {
                if (ownInSuit.Count == 0)
                {
                    return BestDeclarerDiscard(player, leadSuit)[0];
                }

                // Threshold = currently winning card's rank.
                int threshold = (int)currentlyWinning.Card.Rank;
                return FindCheapestStrongerInSuit(player, leadSuit, threshold)
                       ?? ownInSuit[ownInSuit.Count - 1];
            }

            // Defender in 3rd seat.
            if (ownInSuit.Count == 0)
            {
                return BestDefenderDiscard(player, leadSuit)[0];
            }

            int winningPlayer = currentlyWinning.PlayerIndex;
            bool partnerWinning =
                winningPlayer != _state.DeclarerIndex && winningPlayer != player;

            if (partnerWinning)
            {
                return ownInSuit[ownInSuit.Count - 1]; // duck
            }

            // Declarer is winning the trick so far — try to overtake cheaply.
            return FindCheapestStrongerInSuit(
                       player, leadSuit, (int)currentlyWinning.Card.Rank)
                   ?? ownInSuit[ownInSuit.Count - 1];
        }

        // ==================== HELPERS ====================

        private List<PerfectCardMove> GetShortCandidates(int playerIndex)
        {
            return IsDeclarer(playerIndex)
                ? _state.GetDeclarerShortCandidateList()
                : _state.GetDefenderShortCandidateList(playerIndex);
        }

        private List<PerfectCardMove> SmallestCardsInOtherSuits(int playerIndex, Suit leadSuit)
        {
            var result = new List<PerfectCardMove>();
            for (int suitIdx = 0; suitIdx < _state.Moves.Count; suitIdx++)
            {
                if (suitIdx == (int)leadSuit)
                {
                    continue;
                }

                var cards = _state.GetAvailableMovesInSuit(playerIndex, (Suit)suitIdx);
                if (cards.Count > 0)
                {
                    result.Add(cards[cards.Count - 1]); // last == smallest (sorted desc)
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Player {playerIndex} has no cards to discard outside suit {leadSuit}.");
            }

            return result;
        }

        /// <summary>
        /// Returns the player's CHEAPEST available card in the suit whose rank
        /// is strictly greater than <paramref name="rankThreshold"/>, or
        /// <c>null</c> if no such card exists. Suit lists are sorted descending,
        /// so we scan from the end (smallest) towards the start.
        /// </summary>
        private PerfectCardMove? FindCheapestStrongerInSuit(
            int playerIndex, Suit suit, int rankThreshold)
        {
            var suitMoves = _state.Moves[(int)suit];
            for (int i = suitMoves.Count - 1; i >= 0; i--)
            {
                var move = suitMoves[i];
                if (!move.Available || move.PlayerIndex != playerIndex)
                {
                    continue;
                }
                if ((int)move.Card.Rank > rankThreshold)
                {
                    return move;
                }
            }
            return null;
        }

        private static PerfectCardMove CurrentlyWinning(
            PerfectCardMove move1, PerfectCardMove move2)
        {
            if (move2.Card.Suit != move1.Card.Suit)
            {
                return move1;
            }
            return move2.Card.Rank > move1.Card.Rank ? move2 : move1;
        }

        private static int GetTrickWinner(
            PerfectCardMove m1, PerfectCardMove m2, PerfectCardMove m3)
        {
            var winning = CurrentlyWinning(m1, m2);
            if (m3.Card.Suit == m1.Card.Suit && m3.Card.Rank > winning.Card.Rank)
            {
                return m3.PlayerIndex;
            }
            return winning.PlayerIndex;
        }

        private bool IsDeclarer(int playerIndex) => playerIndex == _state.DeclarerIndex;

        private int NextPlayer(int playerIndex) => (playerIndex + 1) % 3;

        private int OtherDefender(int defenderIndex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i != _state.DeclarerIndex && i != defenderIndex)
                {
                    return i;
                }
            }
            throw new InvalidOperationException(
                "Could not determine the other defender (invalid declarer/defender setup).");
        }

        private int LeaderScore(Score3 score, int leader)
            => leader == _state.DeclarerIndex ? score.DeclarerTricks : score.DefendersTricks;

        private int CountRemainingHands()
            => _state.GetAvailableMovesForPlayer(_state.CurrentPlayerIndex).Count();

        private Score3 ZeroScore()
            => Score3.FromThreePlayer(new int[3], _state.DeclarerIndex);
    }
}
