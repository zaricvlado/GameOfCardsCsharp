using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Common
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
    /// Represents the score in a 2 or 3-player Preferance game.
    /// Tracks both coalition scores (Declarer vs Defenders) and individual trick counts.
    /// </summary>
    public readonly struct Score3
    {
        /// <summary>
        /// Total tricks won by declarer(s)
        /// </summary>
        public int DeclarerTricks { get; }

        /// <summary>
        /// Total tricks won by defender(s)
        /// </summary>
        public int DefendersTricks { get; }

        /// <summary>
        /// Individual trick counts for each player [Player0, Player1, Player2]
        /// For 2-player games: Player2's count is 0
        /// </summary>
        public int[] IndividualTricks { get; }

        public Score3(int declarerTricks, int defendersTricks, int[] individualTricks)
        {
            DeclarerTricks = declarerTricks;
            DefendersTricks = defendersTricks;

            if (individualTricks == null || (individualTricks.Length != 2 && individualTricks.Length != 3))
            {
                throw new ArgumentException("IndividualTricks must have 2 or 3 elements", nameof(individualTricks));
            }

            IndividualTricks = new int[3];
            Array.Copy(individualTricks, IndividualTricks, individualTricks.Length);
        }

        /// <summary>
        /// Creates Score3 from a 2-player game
        /// </summary>
        public static Score3 FromTwoPlayer(int player0Tricks, int player1Tricks, int declarerIndex)
        {
            var individual = new int[3];
            individual[0] = player0Tricks;
            individual[1] = player1Tricks;
            individual[2] = 0;

            var declarerTricks = declarerIndex == 0 ? player0Tricks : player1Tricks;
            var defendersTricks = declarerIndex == 0 ? player1Tricks : player0Tricks;

            return new Score3(declarerTricks, defendersTricks, individual);
        }

        /// <summary>
        /// Creates Score3 from a 3-player game
        /// </summary>
        public static Score3 FromThreePlayer(int[] individualTricks, int declarerIndex)
        {
            if (individualTricks == null || individualTricks.Length != 3)
            {
                throw new ArgumentException("Must provide exactly 3 trick counts", nameof(individualTricks));
            }

            var declarerTricks = individualTricks[declarerIndex];
            var defendersTricks = individualTricks.Sum() - declarerTricks;

            return new Score3(declarerTricks, defendersTricks, individualTricks);
        }

        /// <summary>
        /// Increments the trick count for a specific player
        /// </summary>
        public Score3 IncrementPlayer(int playerIndex, int declarerIndex)
        {
            var newIndividual = new int[3];
            Array.Copy(IndividualTricks, newIndividual, 3);
            newIndividual[playerIndex]++;

            return Score3.FromThreePlayer(newIndividual, declarerIndex);
        }

        /// <summary>
        /// Adds two scores together
        /// </summary>
        public Score3 Add(Score3 other)
        {
            var newIndividual = new int[3];
            for (int i = 0; i < 3; i++)
            {
                newIndividual[i] = IndividualTricks[i] + other.IndividualTricks[i];
            }

            return new Score3(
                DeclarerTricks + other.DeclarerTricks,
                DefendersTricks + other.DefendersTricks,
                newIndividual);
        }

        public override string ToString()
        {
            return $"Declarer:{DeclarerTricks} Defenders:{DefendersTricks} " +
                   $"[P0:{IndividualTricks[0]} P1:{IndividualTricks[1]} P2:{IndividualTricks[2]}]";
        }
    }

    /// <summary>
    /// Represents a perfect information game state where all cards are known.
    /// Cards are organized by suit and sorted by rank for efficient analysis.
    /// </summary>
    public class PerfPerfectGameState
    {
        /// <summary>
        /// The game mode (Sans, Trump, or Betl)
        /// </summary>
        public PreferanceGameMode GameMode { get; }

        /// <summary>
        /// The trump suit (TrumpSuit.None for Sans and Betl games)
        /// </summary>
        public TrumpSuit TrumpSuit { get; }

        /// <summary>
        /// List of players in the game
        /// </summary>
        public List<string> Players { get; }

        /// <summary>
        /// Index of the player whose turn it is to play (0-based)
        /// </summary>
        public int CurrentPlayerIndex { get; set; }

        /// <summary>
        /// Index of the player who led the current trick (0-based).
        /// This is the player who played the first card in the trick.
        /// </summary>
        public int LeaderPlayerIndex { get; set; }

        private int _declarerIndex;

        /// <summary>
        /// Index of the player who is the declarer for this hand (0-based).
        /// Mandatory; must always be a valid index into <see cref="Players"/>.
        /// </summary>
        public int DeclarerIndex
        {
            get => _declarerIndex;
            set
            {
                if (value < 0 || value >= Players.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(DeclarerIndex),
                        $"Declarer index {value} is out of range (0-{Players.Count - 1}).");
                }
                _declarerIndex = value;
            }
        }

        /// <summary>
        /// All moves organized by suit. 
        /// Index 0 = Clubs, 1 = Diamonds, 2 = Hearts, 3 = Spades
        /// Each list contains moves from all players, sorted by rank (descending)
        /// </summary>
        public List<List<PerfectCardMove>> Moves { get; }

        public PerfPerfectGameState(
            PreferanceGameMode gameMode,
            List<string> players,
            int declarerIndex,
            TrumpSuit trumpSuit = TrumpSuit.None,
            int currentPlayerIndex = 0,
            int leaderPlayerIndex = 0)
        {
            GameMode = gameMode;
            TrumpSuit = trumpSuit;
            Players = players ?? throw new ArgumentNullException(nameof(players));
            CurrentPlayerIndex = currentPlayerIndex;
            LeaderPlayerIndex = leaderPlayerIndex;

            if (declarerIndex < 0 || declarerIndex >= Players.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(declarerIndex),
                    $"Declarer index {declarerIndex} is out of range (0-{Players.Count - 1}). " +
                    "A declarer must be assigned for every PerfPerfectGameState.");
            }
            DeclarerIndex = declarerIndex;

            // Initialize 4 lists for each suit
            Moves = new List<List<PerfectCardMove>>
            {
                new List<PerfectCardMove>(), // Clubs
                new List<PerfectCardMove>(), // Diamonds
                new List<PerfectCardMove>(), // Hearts
                new List<PerfectCardMove>()  // Spades
            };
        }
        private static int FindSmallestCardIndex(List<PerfectCardMove> suitMoves, bool[] inPlay, int playerIndex)
        {
            // Search from end (smallest cards) towards beginning
            for (int i = suitMoves.Count - 1; i >= 0; i--)
            {
                if (inPlay[i] && suitMoves[i].PlayerIndex == playerIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds the highest card index for a specific player in the suit
        /// </summary>
        private static int FindHighestCardIndex(List<PerfectCardMove> suitMoves, bool[] inPlay, int playerIndex)
        {
            // Search from beginning (highest cards) towards end
            for (int i = 0; i < suitMoves.Count; i++)
            {
                if (inPlay[i] && suitMoves[i].PlayerIndex == playerIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Sets up the game state with specific hands for all players.
        /// Clears any existing cards before adding new ones.
        /// </summary>
        /// <param name="playerHands">Dictionary mapping player index to their hand of cards</param>
        public void SetupPlayerHands(Dictionary<int, List<Card>> playerHands)
        {
            if (playerHands == null)
            {
                throw new ArgumentNullException(nameof(playerHands));
            }

            // Clear existing moves
            foreach (var suitMoves in Moves)
            {
                suitMoves.Clear();
            }

            // Add cards for each player
            foreach (var kvp in playerHands)
            {
                int playerIndex = kvp.Key;
                var hand = kvp.Value;

                if (playerIndex < 0 || playerIndex >= Players.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(playerHands),
                        $"Player index {playerIndex} is out of range (0-{Players.Count - 1})");
                }

                AddPlayerCards(playerIndex, hand);
            }
        }

        /// <summary>
        /// Sets up the game state with hands for all players (overload for 2 players).
        /// Clears any existing cards before adding new ones.
        /// </summary>
        /// <param name="player0Hand">Cards for player 0</param>
        /// <param name="player1Hand">Cards for player 1</param>
        public void SetupPlayerHands(List<Card> player0Hand, List<Card> player1Hand)
        {
            if (Players.Count != 2)
            {
                throw new InvalidOperationException("This overload only works for 2-player games");
            }

            var playerHands = new Dictionary<int, List<Card>>
            {
                { 0, player0Hand },
                { 1, player1Hand }
            };

            SetupPlayerHands(playerHands);
        }

        /// <summary>
        /// Adds cards from a player's hand to the game state.
        /// Cards are automatically sorted by suit and rank.
        /// </summary>
        public void AddPlayerCards(int playerIndex, IEnumerable<Card> cards)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            foreach (var card in cards)
            {
                int suitIndex = (int)card.Suit;
                var suitMoves = Moves[suitIndex];

                var move = new PerfectCardMove(
                    card: card,
                    playerIndex: playerIndex,
                    listIndex: suitMoves.Count, // Will be updated after sorting
                    available: true
                );

                suitMoves.Add(move);
            }

            // Sort each suit by rank (descending - Ace to Two)
            for (int i = 0; i < Moves.Count; i++)
            {
                SortAndReindexSuit(i);
            }
        }

        /// <summary>
        /// Sorts a suit's moves by rank (descending) and updates list indices
        /// </summary>
        private void SortAndReindexSuit(int suitIndex)
        {
            var suitMoves = Moves[suitIndex];

            // Sort by rank descending (Ace > King > Queen > ... > Two)
            var sorted = suitMoves.OrderByDescending(m => m.Card.Rank).ToList();

            // Clear and re-add with correct indices
            suitMoves.Clear();
            for (int i = 0; i < sorted.Count; i++)
            {
                var move = sorted[i];
                var reindexed = new PerfectCardMove(
                    card: move.Card,
                    playerIndex: move.PlayerIndex,
                    listIndex: i,
                    available: move.Available
                );
                suitMoves.Add(reindexed);
            }
        }

        /// <summary>
        /// Advances to the next player's turn
        /// </summary>
        public void AdvanceTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        }

        /// <summary>
        /// Sets a new leader for the trick (typically when starting a new trick)
        /// </summary>
        public void SetLeader(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            }

            LeaderPlayerIndex = playerIndex;
            CurrentPlayerIndex = playerIndex;
        }

        /// <summary>
        /// Gets candidate moves for a specific player.
        /// A candidate is the first available card in each contiguous group of cards belonging to the same player.
        /// For example: if spades are AAABBA, only the first A, last A, and first B are candidates.
        /// </summary>
        public IEnumerable<PerfectCardMove> GetCandidateMovesForPlayer(int playerIndex)
        {
            foreach (var suitMoves in Moves)
            {
                foreach (var candidate in GetCandidateMovesInList(suitMoves, playerIndex))
                {
                    yield return candidate;
                }
            }
        }

        /// <summary>
        /// Gets candidate moves for the current player
        /// </summary>
        public IEnumerable<PerfectCardMove> GetCandidateMovesForCurrentPlayer()
        {
            return GetCandidateMovesForPlayer(CurrentPlayerIndex);
        }

        /// <summary>
        /// Gets candidate moves in a specific suit for a player.
        /// A candidate is the first available card in each contiguous group of cards belonging to the same player.
        /// </summary>
        public IEnumerable<PerfectCardMove> GetCandidateMovesInSuit(int playerIndex, Suit suit)
        {
            int suitIndex = (int)suit;
            return GetCandidateMovesInList(Moves[suitIndex], playerIndex);
        }

        /// <summary>
        /// Identifies candidate moves from a suit list.
        /// A move is a candidate if:
        /// 1. It belongs to the specified player and is available
        /// 2. It's either the first card OR the previous available card belongs to a different player
        /// </summary>
        private IEnumerable<PerfectCardMove> GetCandidateMovesInList(List<PerfectCardMove> suitMoves, int playerIndex)
        {
            int? previousOwner = null;

            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];

                // Skip unavailable cards
                if (!move.Available)
                {
                    continue;
                }

                // Check if this is a candidate
                if (move.PlayerIndex == playerIndex)
                {
                    // It's a candidate if:
                    // - It's the first available card in the list, OR
                    // - The previous available card belonged to a different player
                    if (previousOwner == null || previousOwner != playerIndex)
                    {
                        yield return move;
                    }
                }

                // Update the previous owner
                previousOwner = move.PlayerIndex;
            }
        }

        /// <summary>
        /// Gets all available moves for a specific player (not just candidates)
        /// </summary>
        public IEnumerable<PerfectCardMove> GetAvailableMovesForPlayer(int playerIndex)
        {
            return Moves
                .SelectMany(suitMoves => suitMoves)
                .Where(move => move.Available && move.PlayerIndex == playerIndex);
        }

        /// <summary>
        /// Gets all available moves for the current player
        /// </summary>
        public IEnumerable<PerfectCardMove> GetAvailableMovesForCurrentPlayer()
        {
            return GetAvailableMovesForPlayer(CurrentPlayerIndex);
        }

        /// <summary>
        /// Creates a deep clone of this game state for simulation purposes
        /// </summary>
        public PerfPerfectGameState Clone()
        {
            var clonedMoves = new List<List<PerfectCardMove>>();
            
            foreach (var suitMoves in Moves)
            {
                var clonedSuitMoves = new List<PerfectCardMove>();
                foreach (var move in suitMoves)
                {
                    clonedSuitMoves.Add(new PerfectCardMove(
                        move.Card,
                        move.PlayerIndex,
                        move.ListIndex,
                        move.Available,
                        move.ExpectedTricks != null ? (int[])move.ExpectedTricks.Clone() : null
                    ));
                }
                clonedMoves.Add(clonedSuitMoves);
            }
            
            // Create a new state with cloned data
            var clonedState = new PerfPerfectGameState(
                GameMode,                          // 1st: PreferanceGameMode
                Players.ToList(),                  // 2nd: List<string>
                DeclarerIndex,                     // 3rd: int declarerIndex (mandatory)
                TrumpSuit,                         // 4th: TrumpSuit
                CurrentPlayerIndex,                // 5th: int currentPlayerIndex
                LeaderPlayerIndex                  // 6th: int leaderPlayerIndex
            );
            
            // Replace the empty Moves with our cloned moves
            clonedState.Moves.Clear();
            clonedState.Moves.AddRange(clonedMoves);
            
            return clonedState;
        }

        /// <summary>
        /// Returns a short candidate list for the declarer — at most one card per suit.
        /// Rules per suit (considering only available cards):
        ///   - If the declarer owns the highest card in the suit, that card is the candidate.
        ///   - Otherwise, the candidate is the declarer's second-highest card in the suit.
        ///   - If the declarer has only one card in the suit (and it's not the suit's highest),
        ///     that single card is returned as the candidate.
        ///   - If the declarer has no cards in the suit, no candidate is emitted for that suit.
        /// </summary>
        /// <returns>
        /// A list of up to 4 <see cref="PerfectCardMove"/> entries (one per suit where the
        /// declarer holds at least one available card).
        /// </returns>
        public List<PerfectCardMove> GetDeclarerShortCandidateList()
        {
            var candidates = new List<PerfectCardMove>(4);

            for (int suitIndex = 0; suitIndex < Moves.Count; suitIndex++)
            {
                var candidate = GetDeclarerShortCandidateInSuit(suitIndex);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Returns the declarer's short-list candidate for a single suit, or
        /// <c>null</c> if the declarer has no available cards in that suit.
        /// See <see cref="GetDeclarerShortCandidateList"/> for the full rule set.
        /// </summary>
        public PerfectCardMove? GetDeclarerShortCandidateInSuit(Suit suit)
            => GetDeclarerShortCandidateInSuit((int)suit);

        private PerfectCardMove? GetDeclarerShortCandidateInSuit(int suitIndex)
        {
            var suitMoves = Moves[suitIndex];

            // Find the highest available card in the suit and the declarer's
            // top two available cards in a single pass.
            // Moves are stored sorted by rank descending, so the first available hit
            // is the highest, the next declarer hit is the second-highest, etc.
            PerfectCardMove? suitHighest = null;
            PerfectCardMove? declarerHighest = null;
            PerfectCardMove? declarerSecondHighest = null;

            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];
                if (!move.Available)
                {
                    continue;
                }

                suitHighest ??= move;

                if (move.PlayerIndex == DeclarerIndex)
                {
                    if (declarerHighest == null)
                    {
                        declarerHighest = move;
                    }
                    else if (declarerSecondHighest == null)
                    {
                        declarerSecondHighest = move;
                        // We have everything we could possibly need.
                        break;
                    }
                }
            }

            // Declarer has no cards in this suit.
            if (declarerHighest == null)
            {
                return null;
            }

            // Declarer owns the highest card in the suit -> use it.
            if (suitHighest!.PlayerIndex == DeclarerIndex)
            {
                return declarerHighest;
            }

            // Declarer doesn't own the highest -> prefer second-highest declarer card,
            // fall back to the only declarer card available.
            return declarerSecondHighest ?? declarerHighest;
        }

        /// <summary>
        /// Returns a short candidate list for a defender — at most one card per suit.
        /// Rules per suit (considering only available cards):
        ///   - If the player who plays after <paramref name="defenderIndex"/> is the declarer
        ///     AND this defender does NOT own the suit's highest available card,
        ///     the candidate is this defender's SMALLEST available card in the suit
        ///     (duck through the declarer; either partner wins or we lose minimally).
        ///   - Otherwise, the candidate is this defender's STRONGEST available card in the suit.
        ///   - If this defender has no available cards in the suit, no candidate is emitted.
        /// </summary>
        /// <param name="defenderIndex">
        /// The defender for whom to compute candidates. Must be a valid player index and
        /// must not equal <see cref="DeclarerIndex"/>.
        /// </param>
        public List<PerfectCardMove> GetDefenderShortCandidateList(int defenderIndex)
        {
            if (defenderIndex < 0 || defenderIndex >= Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(defenderIndex),
                    $"Defender index {defenderIndex} is out of range (0-{Players.Count - 1}).");
            }
            if (defenderIndex == DeclarerIndex)
            {
                throw new ArgumentException(
                    "defenderIndex must not equal DeclarerIndex.", nameof(defenderIndex));
            }

            int nextPlayerIndex = (defenderIndex + 1) % Players.Count;
            bool nextPlayerIsDeclarer = nextPlayerIndex == DeclarerIndex;

            var candidates = new List<PerfectCardMove>(4);

            for (int suitIndex = 0; suitIndex < Moves.Count; suitIndex++)
            {
                var candidate = GetDefenderShortCandidateInSuit(
                    suitIndex, defenderIndex, nextPlayerIsDeclarer);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Returns the defender's short-list candidate for a single suit, or
        /// <c>null</c> if the defender has no available cards in that suit.
        /// See <see cref="GetDefenderShortCandidateList(int)"/> for the full rule set.
        /// </summary>
        public PerfectCardMove? GetDefenderShortCandidateInSuit(Suit suit, int defenderIndex)
        {
            int nextPlayerIndex = (defenderIndex + 1) % Players.Count;
            bool nextPlayerIsDeclarer = nextPlayerIndex == DeclarerIndex;
            return GetDefenderShortCandidateInSuit((int)suit, defenderIndex, nextPlayerIsDeclarer);
        }

        private PerfectCardMove? GetDefenderShortCandidateInSuit(
            int suitIndex, int defenderIndex, bool nextPlayerIsDeclarer)
        {
            var suitMoves = Moves[suitIndex];

            // Moves are stored sorted by rank descending. We track:
            //   - the suit's highest available card (first available hit),
            //   - this defender's highest available card (first defender-owned available hit),
            //   - this defender's lowest available card (last defender-owned available hit).
            PerfectCardMove? suitHighest = null;
            PerfectCardMove? defenderHighest = null;
            PerfectCardMove? defenderLowest = null;

            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];
                if (!move.Available)
                {
                    continue;
                }

                suitHighest ??= move;

                if (move.PlayerIndex == defenderIndex)
                {
                    defenderHighest ??= move;
                    defenderLowest = move; // last write wins -> lowest, since list is sorted desc
                }
            }

            // Defender has no cards in this suit.
            if (defenderHighest == null)
            {
                return null;
            }

            // If declarer plays right after us and we don't own the suit's highest,
            // duck with our smallest in that suit.
            bool defenderOwnsSuitHighest = suitHighest!.PlayerIndex == defenderIndex;
            if (nextPlayerIsDeclarer && !defenderOwnsSuitHighest)
            {
                return defenderLowest;
            }

            // Otherwise, play the defender's strongest card in the suit.
            return defenderHighest;
        }

        /// <summary>
        /// Returns the player's highest-ranked available card in the given suit,
        /// or <c>null</c> if the player has no available cards in that suit.
        /// </summary>
        /// <remarks>
        /// Moves are stored sorted by rank descending, so the first available hit
        /// for the player is their highest card.
        /// </remarks>
        public PerfectCardMove? GetHighestAvailableMoveInSuit(int playerIndex, Suit suit)
        {
            ValidatePlayerIndex(playerIndex);

            var suitMoves = Moves[(int)suit];
            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];
                if (move.Available && move.PlayerIndex == playerIndex)
                {
                    return move;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all available moves the player holds in the given suit, sorted by
        /// rank descending (highest first). Returns an empty list when the player
        /// has no available cards in that suit.
        /// </summary>
        public IReadOnlyList<PerfectCardMove> GetAvailableMovesInSuit(int playerIndex, Suit suit)
        {
            ValidatePlayerIndex(playerIndex);

            var suitMoves = Moves[(int)suit];
            var result = new List<PerfectCardMove>();
            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];
                if (move.Available && move.PlayerIndex == playerIndex)
                {
                    result.Add(move);
                }
            }

            return result;
        }

        private void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= Players.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex),
                    $"Player index {playerIndex} is out of range (0-{Players.Count - 1}).");
            }
        }

        /// <summary>
        /// Analyzes a single suit for a discard decision in a 1-vs-1 model:
        /// the <paramref name="attackerIndex"/> leads the suit repeatedly (highest
        /// first); the <paramref name="defenderIndex"/> responds with the smallest
        /// card that beats the lead, otherwise the smallest card. Runs the
        /// simulation twice — with and without the defender's lowest card — so a
        /// policy can score the cost of discarding it.
        ///
        /// Suitable for 2-player games and for the declarer side in 3-player
        /// games (where modeling partner cooperation is not applicable). For
        /// 3-player defender discards prefer
        /// <see cref="AnalyzeSuitDiscard3P(Suit, int, int, int)"/>.
        /// </summary>
        public SuitDiscardAnalysis AnalyzeSuitDiscard2P(
            Suit suit, int attackerIndex, int defenderIndex)
        {
            ValidatePlayerIndex(attackerIndex);
            ValidatePlayerIndex(defenderIndex);
            if (attackerIndex == defenderIndex)
            {
                throw new ArgumentException(
                    "attackerIndex and defenderIndex must differ.", nameof(defenderIndex));
            }

            var suitMoves = Moves[(int)suit];

            // Single pass: per-player counts, strongest available, defender's lowest.
            var counts = new int[Players.Count];
            PerfectCardMove? strongest = null;
            PerfectCardMove? candidate = null;

            for (int i = 0; i < suitMoves.Count; i++)
            {
                var m = suitMoves[i];
                if (!m.Available)
                {
                    continue;
                }

                strongest ??= m;
                counts[m.PlayerIndex]++;

                if (m.PlayerIndex == defenderIndex)
                {
                    // Last write wins -> lowest, since the suit list is sorted desc.
                    candidate = m;
                }
            }

            var with = SimulateDefense2P(
                suitMoves, attackerIndex, defenderIndex, excludeListIndex: -1);

            var without = SimulateDefense2P(
                suitMoves, attackerIndex, defenderIndex,
                excludeListIndex: candidate?.ListIndex ?? -1);

            return new SuitDiscardAnalysis(
                suit, candidate, with, without, strongest, counts);
        }

        /// <summary>
        /// 1-vs-1 simulation of a single suit from the defender's perspective.
        /// The attacker leads its highest available card; the defender responds
        /// with the smallest card that beats the lead, otherwise the smallest
        /// card. Loop ends when either side runs out of the suit. Optionally
        /// excludes one of the defender's cards (used to compute the "without
        /// candidate" outcome).
        /// </summary>
        private static SuitDefenseOutcome SimulateDefense2P(
            List<PerfectCardMove> suitMoves,
            int attackerIndex,
            int defenderIndex,
            int excludeListIndex)
        {
            var inPlay = new bool[suitMoves.Count];
            for (int i = 0; i < suitMoves.Count; i++)
            {
                var m = suitMoves[i];
                if (!m.Available)
                {
                    continue;
                }
                if (m.ListIndex == excludeListIndex && m.PlayerIndex == defenderIndex)
                {
                    continue;
                }
                if (m.PlayerIndex == attackerIndex || m.PlayerIndex == defenderIndex)
                {
                    inPlay[i] = true;
                }
            }

            int sureTricks = 0;

            while (true)
            {
                int leadIdx = FindHighestCardIndex(suitMoves, inPlay, attackerIndex);
                if (leadIdx == -1)
                {
                    break;
                }

                int leadRank = (int)suitMoves[leadIdx].Card.Rank;
                int replyIdx = FindSmallestHigherCardIndex(
                    suitMoves, inPlay, defenderIndex, leadRank);

                if (replyIdx != -1)
                {
                    sureTricks++;
                    inPlay[leadIdx] = false;
                    inPlay[replyIdx] = false;
                    continue;
                }

                int smallestDefIdx = FindSmallestCardIndex(suitMoves, inPlay, defenderIndex);
                inPlay[leadIdx] = false;
                if (smallestDefIdx == -1)
                {
                    break;
                }
                inPlay[smallestDefIdx] = false;
            }

            int lengthTricks = 0;
            for (int i = 0; i < suitMoves.Count; i++)
            {
                if (inPlay[i] && suitMoves[i].PlayerIndex == defenderIndex)
                {
                    lengthTricks++;
                }
            }

            return new SuitDefenseOutcome(sureTricks, lengthTricks);
        }

        /// <summary>
        /// Analyzes a single suit for a discard decision in a 3-player coalition
        /// model. <c>SureTricks</c> stays strictly attributed to <paramref name="defenderIndex"/>
        /// (defenderA — the candidate-holder). <paramref name="partnerIndex"/>
        /// cooperates on the same side as defenderA against the declarer but
        /// does not contribute to defenderA's sure-trick count even when partner
        /// happens to win the trick.
        ///
        /// The side on which <paramref name="partnerIndex"/> plays is derived
        /// automatically from <see cref="DeclarerIndex"/>:
        /// when <paramref name="defenderIndex"/> is the declarer, partner is on
        /// the <i>attacker</i> side (two defenders attack the declarer); otherwise
        /// partner is on the <i>defender</i> side (helping defenderA against the
        /// declarer who leads).
        /// </summary>
        public SuitDiscardAnalysis AnalyzeSuitDiscard3P(
            Suit suit, int attackerIndex, int defenderIndex)
        {
            if (Players.Count != 3)
            {
                throw new InvalidOperationException(
                    "AnalyzeSuitDiscard3P requires a 3-player game state.");
            }

            ValidatePlayerIndex(attackerIndex);
            ValidatePlayerIndex(defenderIndex);
            if (attackerIndex == defenderIndex)
            {
                throw new ArgumentException(
                    "attackerIndex and defenderIndex must differ.", nameof(defenderIndex));
            }

            int partnerIndex = 3 - attackerIndex - defenderIndex; // 0+1+2 = 3

            var suitMoves = Moves[(int)suit];

            var counts = new int[Players.Count];
            PerfectCardMove? strongest = null;
            PerfectCardMove? candidate = null;

            for (int i = 0; i < suitMoves.Count; i++)
            {
                var m = suitMoves[i];
                if (!m.Available)
                {
                    continue;
                }

                strongest ??= m;
                counts[m.PlayerIndex]++;

                if (m.PlayerIndex == defenderIndex)
                {
                    candidate = m;
                }
            }

            var with = SimulateDefense3P(
                suitMoves, attackerIndex, defenderIndex, partnerIndex,
                excludeListIndex: -1);

            var without = SimulateDefense3P(
                suitMoves, attackerIndex, defenderIndex, partnerIndex,
                excludeListIndex: candidate?.ListIndex ?? -1);

            return new SuitDiscardAnalysis(
                suit, candidate, with, without, strongest, counts);
        }

        /// <summary>
        /// 3-player simulation. Partner side is decided from
        /// <see cref="DeclarerIndex"/>: when <paramref name="defenderIndex"/> is
        /// the declarer the partner is on the attacker side, otherwise on the
        /// defender side. <c>SureTricks</c> is attributed strictly to
        /// <paramref name="defenderIndex"/>.
        /// </summary>
        // ============================================================
        // Bitmask representation of a single suit's cards
        // ------------------------------------------------------------
        // suitMoves is sorted by rank DESCENDING, so for a Preferance
        // suit the indices map to ranks like this:
        //
        //   index : 0  1  2  3  4   5  6  7
        //   rank  : A  K  Q  J  10  9  8  7
        //
        // For each player we keep a uint where bit i is set iff that
        // player currently still holds suitMoves[i]. Therefore:
        //   - lowest set bit  (TrailingZeroCount)         = HIGHEST rank
        //   - highest set bit (31 - LeadingZeroCount)     = LOWEST  rank
        //   - "cards outranking suitMoves[leadIdx]"       = bits below leadIdx
        //   - "play card i"                               = clear bit i
        //   - "how many cards left"                       = PopCount
        //
        // The helper methods below give these bit operations readable
        // names; they are aggressively inlined so there is no runtime
        // cost vs. writing the bit-twiddling inline.
        // ============================================================

        /// <summary>Index of the player's highest-rank card in the suit (-1 if none).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HighestIndex(uint cards)
            => cards == 0 ? -1 : BitOperations.TrailingZeroCount(cards);

        /// <summary>Index of the player's lowest-rank card in the suit (-1 if none).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LowestIndex(uint cards)
            => cards == 0 ? -1 : 31 - BitOperations.LeadingZeroCount(cards);

        /// <summary>
        /// Subset of <paramref name="cards"/> whose rank is strictly higher than
        /// the card at <paramref name="leadIdx"/> (i.e. indices below it).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HigherThan(uint cards, int leadIdx)
            => cards & ((1u << leadIdx) - 1u);

        /// <summary>Plays (removes) the card at <paramref name="idx"/> from the hand.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Remove(ref uint cards, int idx)
            => cards &= ~(1u << idx);

        /// <summary>Number of cards remaining in the hand.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Count(uint cards)
            => BitOperations.PopCount(cards);

        /// <summary>
        /// Builds the per-player suit bitmasks used by <see cref="SimulateDefense3P"/>.
        /// Only available cards are included; if <paramref name="excludeListIndex"/>
        /// matches one of the defender's cards, that card is skipped (used to compute
        /// the "without candidate" outcome).
        /// </summary>
        private static void InitializeAvailableCards(
            List<PerfectCardMove> suitMoves,
            int attackerIndex,
            int defenderIndex,
            int partnerIndex,
            int excludeListIndex,
            out uint attackerCards,
            out uint defenderCards,
            out uint partnerCards)
        {
            attackerCards = 0u;
            defenderCards = 0u;
            partnerCards = 0u;

            int count = suitMoves.Count;
            for (int i = 0; i < count; i++)
            {
                var m = suitMoves[i];
                if (!m.Available)
                {
                    continue;
                }

                int pi = m.PlayerIndex;
                uint bit = 1u << i;

                if (pi == defenderIndex)
                {
                    if (m.ListIndex == excludeListIndex)
                    {
                        continue;
                    }
                    defenderCards |= bit;
                }
                else if (pi == attackerIndex)
                {
                    attackerCards |= bit;
                }
                else if (pi == partnerIndex)
                {
                    partnerCards |= bit;
                }
            }
        }

        /// <summary>
        /// 3-player simulation. Partner side is decided from
        /// <see cref="DeclarerIndex"/>: when <paramref name="defenderIndex"/> is
        /// the declarer the partner is on the attacker side, otherwise on the
        /// defender side. <c>SureTricks</c> is attributed strictly to
        /// <paramref name="defenderIndex"/>.
        ///
        /// Implementation uses per-player suit bitmasks (see the comment block
        /// above <see cref="HighestIndex"/>). Reads almost like English once the
        /// "lower index = higher rank" rule is internalized.
        /// </summary>
        private SuitDefenseOutcome SimulateDefense3P(
            List<PerfectCardMove> suitMoves,
            int attackerIndex,
            int defenderIndex,
            int partnerIndex,
            int excludeListIndex)
        {
            // attackerCards / defenderCards / partnerCards:
            // bit i set => that player still holds suitMoves[i].
            InitializeAvailableCards(
                suitMoves,
                attackerIndex, defenderIndex, partnerIndex,
                excludeListIndex,
                out uint attackerCards,
                out uint defenderCards,
                out uint partnerCards);

            bool partnerOnAttackerSide = defenderIndex == DeclarerIndex;
            int sureTricks = 0;

            if (!partnerOnAttackerSide)
            {
                // ===== Case A: single attacker (declarer) leads; partner helps defenderA =====
                while (attackerCards != 0)
                {
                    int leadIdx = HighestIndex(attackerCards);
                    Remove(ref attackerCards, leadIdx);

                    uint defenderHigherThanLead = HigherThan(defenderCards, leadIdx);
                    if (defenderHigherThanLead != 0)
                    {
                        // DefenderA beats the lead with its smallest-such card.
                        int defReplyIdx = LowestIndex(defenderHigherThanLead);
                        Remove(ref defenderCards, defReplyIdx);

                        // Partner plays its smallest (realistic, not tactical).
                        int partnerSmallIdx = LowestIndex(partnerCards);
                        if (partnerSmallIdx != -1)
                        {
                            Remove(ref partnerCards, partnerSmallIdx);

                            // Lower index = higher rank. Partner "accidentally wins"
                            // only if its smallest still outranks defenderA's reply
                            // (partnerSmallIdx < defReplyIdx). Otherwise defenderA wins.
                            if (partnerSmallIdx >= defReplyIdx)
                            {
                                sureTricks++;
                            }
                        }
                        else
                        {
                            sureTricks++;
                        }
                        continue;
                    }

                    // DefenderA cannot beat the lead -> discards smallest (if any).
                    int defSmallIdx = LowestIndex(defenderCards);
                    if (defSmallIdx != -1)
                    {
                        Remove(ref defenderCards, defSmallIdx);
                    }

                    // Partner tries to rescue: smallest card outranking the lead.
                    uint partnerHigherThanLead = HigherThan(partnerCards, leadIdx);
                    if (partnerHigherThanLead != 0)
                    {
                        int partnerReplyIdx = LowestIndex(partnerHigherThanLead);
                        Remove(ref partnerCards, partnerReplyIdx);
                        // Partner wins the trick -> no credit for defenderA.
                    }
                    else
                    {
                        // Partner cannot rescue -> discards smallest (declarer wins).
                        int partnerSmallIdx = LowestIndex(partnerCards);
                        if (partnerSmallIdx != -1)
                        {
                            Remove(ref partnerCards, partnerSmallIdx);
                        }
                    }
                }
            }
            else
            {
                // ===== Case B: two-defender attacking coalition leads; declarer is candidate-holder =====
                while (attackerCards != 0 || partnerCards != 0)
                {
                    int attackerHighIdx = HighestIndex(attackerCards);
                    int partnerHighIdx = HighestIndex(partnerCards);

                    // Pick whichever side has the higher card to lead.
                    // Lower index = higher rank; ties go to the attacker (matches
                    // the original >= preference).
                    int leadIdx;
                    if (partnerHighIdx == -1
                        || (attackerHighIdx != -1 && attackerHighIdx <= partnerHighIdx))
                    {
                        leadIdx = attackerHighIdx;
                        Remove(ref attackerCards, leadIdx);
                        // Other side (partner) plays its smallest if any.
                        int partnerSmallIdx = LowestIndex(partnerCards);
                        if (partnerSmallIdx != -1)
                        {
                            Remove(ref partnerCards, partnerSmallIdx);
                        }
                    }
                    else
                    {
                        leadIdx = partnerHighIdx;
                        Remove(ref partnerCards, leadIdx);
                        // Other side (attacker) plays its smallest if any.
                        int attackerSmallIdx = LowestIndex(attackerCards);
                        if (attackerSmallIdx != -1)
                        {
                            Remove(ref attackerCards, attackerSmallIdx);
                        }
                    }

                    // Declarer (candidate-holder) reply.
                    uint declarerHigherThanLead = HigherThan(defenderCards, leadIdx);
                    if (declarerHigherThanLead != 0)
                    {
                        int declarerReplyIdx = LowestIndex(declarerHigherThanLead);
                        Remove(ref defenderCards, declarerReplyIdx);
                        sureTricks++;
                    }
                    else
                    {
                        int declarerSmallIdx = LowestIndex(defenderCards);
                        if (declarerSmallIdx != -1)
                        {
                            Remove(ref defenderCards, declarerSmallIdx);
                        }
                        // Otherwise declarer is void -> defenders win uncontested.
                    }
                }
            }

            // Cards defenderA never had to play in this suit are potential length tricks.
            int lengthTricks = Count(defenderCards);

            return new SuitDefenseOutcome(sureTricks, lengthTricks);
        }

        /// <summary>
        /// Finds the index of <paramref name="playerIndex"/>'s SMALLEST available
        /// card in the suit whose rank is strictly greater than
        /// <paramref name="rankThreshold"/>. Returns -1 when no such card exists.
        /// Suit lists are sorted descending, so we scan from the end (smallest)
        /// toward the start.
        /// </summary>
        private static int FindSmallestHigherCardIndex(
            List<PerfectCardMove> suitMoves, bool[] inPlay, int playerIndex, int rankThreshold)
        {
            for (int i = suitMoves.Count - 1; i >= 0; i--)
            {
                if (!inPlay[i]) continue;
                if (suitMoves[i].PlayerIndex != playerIndex) continue;
                if ((int)suitMoves[i].Card.Rank > rankThreshold)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}