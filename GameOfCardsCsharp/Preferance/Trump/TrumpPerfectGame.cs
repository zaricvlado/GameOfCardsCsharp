using System;
using System.Collections.Generic;
using System.Linq;
using GameOfCardsCsharp.Preferance.Common;

namespace GameOfCardsCsharp.Preferance.Trump
{
    /// <summary>
    /// Perfect information game for 3-player Preferance Trump play.
    /// One declarer vs. two cooperating defenders, with a trump suit.
    /// Mirrors the structure of <see cref="Sans.SansPerfectGame"/>;
    /// trump-specific behavior is confined to <see cref="_trumpSuit"/>, 
    /// the trick-winner helpers, and the second/third seat follow logic.
    /// </summary>
    public class TrumpPerfectGame
    {
        private readonly PerfPerfectGameState _state;
        private readonly Suit _trumpSuit;

        public PerfPerfectGameState State => _state;

        public TrumpPerfectGame(PerfPerfectGameState state)
        {
            if (state.Players.Count != 3)
            {
                throw new ArgumentException(
                    "TrumpPerfectGame only supports 3 players", nameof(state));
            }

            if (state.GameMode != PreferanceGameMode.Trump)
            {
                throw new ArgumentException(
                    "TrumpPerfectGame requires Trump game mode", nameof(state));
            }

            if (state.TrumpSuit == TrumpSuit.None)
            {
                throw new ArgumentException(
                    "TrumpPerfectGame requires a trump suit", nameof(state));
            }

            _state = state;
            _trumpSuit = TrumpSuitToSuit(state.TrumpSuit);
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
        /// Initial discard heuristic for the declarer when unable to follow suit:
        /// returns the smallest available card from each of the other non-trump suits.
        /// </summary>
        public List<PerfectCardMove> BestDeclarerDiscard(int playerIndex, Suit leadSuit)
            => SmallestCardsInOtherSuits(playerIndex, leadSuit);

        /// <summary>
        /// Initial discard heuristic for a defender when unable to follow suit:
        /// returns the smallest available card from each of the other non-trump suits.
        /// </summary>
        public List<PerfectCardMove> BestDefenderDiscard(int playerIndex, Suit leadSuit)
            => SmallestCardsInOtherSuits(playerIndex, leadSuit);

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

        /// <summary>
        /// Plays a move and updates the game state permanently.
        /// </summary>
        public void PlayMove(PerfectCardMove move)
        {
            _state.Moves[(int)move.Card.Suit][move.ListIndex].Available = false;
            _state.AdvanceTurn();
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
            int thirdSeat = NextPlayer(player);

            if (IsDeclarer(player))
            {
                // ---- Declarer in 2nd seat ----
                if (ownInSuit.Count > 0)
                {
                    // 1a) Declarer has lead suit.
                    var thirdHigh = _state.GetHighestAvailableMoveInSuit(thirdSeat, leadSuit);

                    if (thirdHigh == null)
                    {
                        // 3rd seat is void of lead suit. If they can ruff, it's
                        // pointless to commit a strong card -> play smallest in suit.
                        if (leadSuit != _trumpSuit && HasTrump(thirdSeat))
                        {
                            return ownInSuit[ownInSuit.Count - 1];
                        }
                        // 3rd seat will discard or follow non-trump only:
                        // try cheapest stronger than move1, else smallest in suit.
                        return FindCheapestStrongerInSuit(player, leadSuit, (int)move1.Card.Rank)
                               ?? ownInSuit[ownInSuit.Count - 1];
                    }

                    // 3rd seat will follow suit. Threshold is the higher of move1 and
                    // 3rd seat's strongest in suit.
                    int threshold = (int)move1.Card.Rank;
                    if ((int)thirdHigh.Card.Rank > threshold)
                    {
                        threshold = (int)thirdHigh.Card.Rank;
                    }
                    return FindCheapestStrongerInSuit(player, leadSuit, threshold)
                           ?? ownInSuit[ownInSuit.Count - 1];
                }

                // 1b) Declarer void of lead suit.
                if (HasTrump(player))
                {
                    // If 3rd seat can follow lead suit, our trump always wins -> smallest trump.
                    if (_state.GetHighestAvailableMoveInSuit(thirdSeat, leadSuit) != null)
                    {
                        return SmallestInSuit(player, _trumpSuit)!;
                    }

                    // 3rd seat is also void of lead suit; if they have trump,
                    // try to overtrump their best.
                    var thirdHighTrump = _state.GetHighestAvailableMoveInSuit(thirdSeat, _trumpSuit);
                    if (thirdHighTrump != null)
                    {
                        return FindCheapestStrongerInSuit(player, _trumpSuit, (int)thirdHighTrump.Card.Rank)
                               ?? SmallestInSuit(player, _trumpSuit)!;
                    }

                    // 3rd seat has no trump and no lead suit -> they will discard.
                    return SmallestInSuit(player, _trumpSuit)!;
                }

                return BestDeclarerDiscard(player, leadSuit)[0];
            }

            // ---- Defender in 2nd seat ----
            bool firstSeatIsDeclarer = move1.PlayerIndex == _state.DeclarerIndex;

            if (ownInSuit.Count > 0)
            {
                // 2a) Defender has lead suit.
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

                    if (otherHigh != null)
                    {
                        // If the other defender can beat the lead with a card cheaper
                        // than ours, let them win — duck with smallest.
                        if ((int)otherHigh.Card.Rank > (int)move1.Card.Rank
                            && (int)otherHigh.Card.Rank < (int)candidate.Card.Rank)
                        {
                            return ownInSuit[ownInSuit.Count - 1];
                        }
                        return candidate;
                    }

                    // Partner is void of lead suit.
                    if (leadSuit != _trumpSuit && HasTrump(otherDef))
                    {
                        // Partner will ruff; duck with smallest in suit.
                        return ownInSuit[ownInSuit.Count - 1];
                    }
                    return candidate;
                }
                else
                {
                    // 2a2: other defender led, declarer is in 3rd seat.
                    var declHigh = _state.GetHighestAvailableMoveInSuit(
                        _state.DeclarerIndex, leadSuit);

                    if (declHigh != null)
                    {
                        if ((int)declHigh.Card.Rank > (int)move1.Card.Rank)
                        {
                            // Declarer can beat partner — try to overtake.
                            return FindCheapestStrongerInSuit(player, leadSuit, (int)move1.Card.Rank)
                                   ?? ownInSuit[ownInSuit.Count - 1];
                        }
                        // Declarer cannot beat partner in suit; declarer must follow
                        // suit so cannot ruff either — duck with smallest.
                        return ownInSuit[ownInSuit.Count - 1];
                    }

                    // Declarer is void of lead suit.
                    if (leadSuit != _trumpSuit && HasTrump(_state.DeclarerIndex))
                    {
                        // Declarer will ruff; duck with smallest.
                        return ownInSuit[ownInSuit.Count - 1];
                    }
                    // Declarer has no trump and is void of suit: play LARGEST in suit
                    // so we can win and "go through" the declarer next move.
                    return ownInSuit[0];
                }
            }

            // 2b) Defender void of lead suit.
            if (HasTrump(player))
            {
                if (firstSeatIsDeclarer)
                {
                    // 2b1: smallest trump dominates — partner plays last and any
                    // larger trump would just be over-trumped by partner anyway.
                    return SmallestInSuit(player, _trumpSuit)!;
                }

                // 2b2: partner led, declarer plays last.
                int declIdx = _state.DeclarerIndex;
                bool declHasLead = _state.GetHighestAvailableMoveInSuit(declIdx, leadSuit) != null;
                bool declHasTrump = HasTrump(declIdx);

                if (declHasLead || !declHasTrump)
                {
                    return SmallestInSuit(player, _trumpSuit)!;
                }

                // Declarer void of lead suit and has trump -> try to overtrump
                // declarer's best trump.
                var declHighTrump = _state.GetHighestAvailableMoveInSuit(declIdx, _trumpSuit)!;
                return FindCheapestStrongerInSuit(player, _trumpSuit, (int)declHighTrump.Card.Rank)
                       ?? SmallestInSuit(player, _trumpSuit)!;
            }

            return BestDefenderDiscard(player, leadSuit)[0];
        }

        private PerfectCardMove PickThirdSeatFollow(
            PerfectCardMove move1,
            PerfectCardMove move2,
            int player,
            Suit leadSuit,
            IReadOnlyList<PerfectCardMove> ownInSuit)
        {
            PerfectCardMove currentlyWinning = CurrentlyWinning(move1, move2);
            bool winningIsTrump = currentlyWinning.Card.Suit == _trumpSuit;

            if (IsDeclarer(player))
            {
                // ---- Declarer in 3rd seat ----
                if (ownInSuit.Count > 0)
                {
                    // 1a) Has lead suit -> must follow suit, cannot ruff.
                    if (winningIsTrump && leadSuit != _trumpSuit)
                    {
                        // Trick is lost in suit; play smallest.
                        return ownInSuit[ownInSuit.Count - 1];
                    }

                    // Winning card is in lead suit; try to overtake.
                    return FindCheapestStrongerInSuit(player, leadSuit, (int)currentlyWinning.Card.Rank)
                           ?? ownInSuit[ownInSuit.Count - 1];
                }

                // 1b) Void of lead suit.
                if (HasTrump(player))
                {
                    if (winningIsTrump)
                    {
                        return FindCheapestStrongerInSuit(player, _trumpSuit, (int)currentlyWinning.Card.Rank)
                               ?? SmallestInSuit(player, _trumpSuit)!;
                    }
                    // Winning is in lead suit; any trump wins -> smallest trump.
                    return SmallestInSuit(player, _trumpSuit)!;
                }

                return BestDeclarerDiscard(player, leadSuit)[0];
            }

            // ---- Defender in 3rd seat ----
            int winningPlayer = currentlyWinning.PlayerIndex;
            bool partnerWinning =
                winningPlayer != _state.DeclarerIndex && winningPlayer != player;

            if (ownInSuit.Count > 0)
            {
                // 2a) Has lead suit -> must follow suit.
                if (partnerWinning)
                {
                    // Partner's win is final (declarer already played) — duck.
                    return ownInSuit[ownInSuit.Count - 1];
                }

                // Declarer is winning the trick.
                if (winningIsTrump && leadSuit != _trumpSuit)
                {
                    // Cannot beat trump with a non-trump card; duck.
                    return ownInSuit[ownInSuit.Count - 1];
                }
                return FindCheapestStrongerInSuit(player, leadSuit, (int)currentlyWinning.Card.Rank)
                       ?? ownInSuit[ownInSuit.Count - 1];
            }

            // 2b) Void of lead suit.
            if (partnerWinning)
            {
                // With full info, partner's win is final — don't waste a trump.
                return BestDefenderDiscard(player, leadSuit)[0];
            }

            if (HasTrump(player))
            {
                // Declarer is winning.
                if (winningIsTrump)
                {
                    return FindCheapestStrongerInSuit(player, _trumpSuit, (int)currentlyWinning.Card.Rank)
                           ?? SmallestInSuit(player, _trumpSuit)!;
                }
                // Any trump beats a non-trump winner -> smallest trump.
                return SmallestInSuit(player, _trumpSuit)!;
            }

            return BestDefenderDiscard(player, leadSuit)[0];
        }

        // ==================== HELPERS ====================

        private List<PerfectCardMove> GetShortCandidates(int playerIndex)
        {
            return IsDeclarer(playerIndex)
                ? _state.GetDeclarerShortCandidateList()
                : _state.GetDefenderShortCandidateList(playerIndex);
        }

        /// <summary>
        /// Returns the smallest available card from each suit that is neither
        /// the lead suit nor the trump suit. If lead suit == trump suit, only
        /// that single suit is excluded.
        /// </summary>
        private List<PerfectCardMove> SmallestCardsInOtherSuits(int playerIndex, Suit leadSuit)
        {
            var result = new List<PerfectCardMove>();
            for (int suitIdx = 0; suitIdx < _state.Moves.Count; suitIdx++)
            {
                if (suitIdx == (int)leadSuit)
                {
                    continue;
                }
                if (suitIdx == (int)_trumpSuit)
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
                    $"Player {playerIndex} has no cards to discard outside suit {leadSuit} and trump {_trumpSuit}.");
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

        private PerfectCardMove? SmallestInSuit(int playerIndex, Suit suit)
        {
            var list = _state.GetAvailableMovesInSuit(playerIndex, suit);
            return list.Count == 0 ? null : list[list.Count - 1];
        }

        private bool HasTrump(int playerIndex)
            => _state.GetHighestAvailableMoveInSuit(playerIndex, _trumpSuit) != null;

        /// <summary>
        /// Determines which of the first two played cards is currently winning,
        /// honoring trump rules (trump beats non-trump; otherwise highest in
        /// lead suit wins; off-suit non-trump cannot win).
        /// </summary>
        private PerfectCardMove CurrentlyWinning(PerfectCardMove move1, PerfectCardMove move2)
        {
            bool m1Trump = move1.Card.Suit == _trumpSuit;
            bool m2Trump = move2.Card.Suit == _trumpSuit;

            if (m1Trump && !m2Trump) return move1;
            if (!m1Trump && m2Trump) return move2;
            if (m1Trump && m2Trump)
            {
                return move2.Card.Rank > move1.Card.Rank ? move2 : move1;
            }
            // Both non-trump.
            if (move2.Card.Suit != move1.Card.Suit) return move1;
            return move2.Card.Rank > move1.Card.Rank ? move2 : move1;
        }

        /// <summary>
        /// Determines the winner of a 3-card trick honoring trump rules.
        /// </summary>
        private int GetTrickWinner(PerfectCardMove m1, PerfectCardMove m2, PerfectCardMove m3)
        {
            var winning = CurrentlyWinning(m1, m2);
            bool wTrump = winning.Card.Suit == _trumpSuit;
            bool m3Trump = m3.Card.Suit == _trumpSuit;

            if (!wTrump && m3Trump) return m3.PlayerIndex;
            if (wTrump && !m3Trump) return winning.PlayerIndex;
            if (wTrump && m3Trump)
            {
                return m3.Card.Rank > winning.Card.Rank ? m3.PlayerIndex : winning.PlayerIndex;
            }
            // Both non-trump.
            if (m3.Card.Suit == m1.Card.Suit && m3.Card.Rank > winning.Card.Rank)
            {
                return m3.PlayerIndex;
            }
            return winning.PlayerIndex;
        }

        private static Suit TrumpSuitToSuit(TrumpSuit t) => t switch
        {
            TrumpSuit.Clubs => Suit.Clubs,
            TrumpSuit.Diamonds => Suit.Diamonds,
            TrumpSuit.Hearts => Suit.Hearts,
            TrumpSuit.Spades => Suit.Spades,
            _ => throw new ArgumentException("Invalid trump suit", nameof(t))
        };

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
