using System;
using System.Collections.Generic;
using System.Linq;
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

            // Validation
            if (DeclarerTricks + DefendersTricks != individualTricks.Sum())
            {
                throw new ArgumentException("DeclarerTricks + DefendersTricks must equal sum of IndividualTricks");
            }
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

            return FromThreePlayer(newIndividual, declarerIndex);
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

        /// <summary>
        /// All moves organized by suit. 
        /// Index 0 = Clubs, 1 = Diamonds, 2 = Hearts, 3 = Spades
        /// Each list contains moves from all players, sorted by rank (descending)
        /// </summary>
        public List<List<PerfectCardMove>> Moves { get; }

        public PerfPerfectGameState(PreferanceGameMode gameMode, List<string> players, TrumpSuit trumpSuit = TrumpSuit.None, int currentPlayerIndex = 0, int leaderPlayerIndex = 0)
        {
            GameMode = gameMode;
            TrumpSuit = trumpSuit;
            Players = players ?? throw new ArgumentNullException(nameof(players));
            CurrentPlayerIndex = currentPlayerIndex;
            LeaderPlayerIndex = leaderPlayerIndex;
            
            // Initialize 4 lists for each suit
            Moves = new List<List<PerfectCardMove>>
            {
                new List<PerfectCardMove>(), // Clubs
                new List<PerfectCardMove>(), // Diamonds
                new List<PerfectCardMove>(), // Hearts
                new List<PerfectCardMove>()  // Spades
            };
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
                .Where(move => move.PlayerIndex == playerIndex && move.Available);
        }

        /// <summary>
        /// Gets available moves in a specific suit for a player (not just candidates)
        /// </summary>
        public IEnumerable<PerfectCardMove> GetAvailableMovesInSuit(int playerIndex, Suit suit)
        {
            int suitIndex = (int)suit;
            return Moves[suitIndex]
                .Where(move => move.PlayerIndex == playerIndex && move.Available);
        }

        /// <summary>
        /// Sorts a suit's moves by rank (descending) and updates list indices
        /// </summary>
        private void SortAndReindexSuit(int suitIndex)
        {
            var suitMoves = Moves[suitIndex];
            
            // Sort by rank descending (Ace first, Two last)
            suitMoves.Sort((a, b) => b.Card.Rank.CompareTo(a.Card.Rank));

            // Update list indices
            for (int i = 0; i < suitMoves.Count; i++)
            {
                var move = suitMoves[i];
                move.ListIndex = i;
                suitMoves[i] = move;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Game Mode: {GameMode}");
            sb.AppendLine($"Players: {string.Join(", ", Players)}");
            sb.AppendLine($"Current Turn: {Players[CurrentPlayerIndex]} (Index: {CurrentPlayerIndex})");
            sb.AppendLine($"Trick Leader: {Players[LeaderPlayerIndex]} (Index: {LeaderPlayerIndex})");
            sb.AppendLine();

            var suitNames = new[] { "Clubs", "Diamonds", "Hearts", "Spades" };
            for (int i = 0; i < Moves.Count; i++)
            {
                sb.AppendLine($"{suitNames[i]}:");
                foreach (var move in Moves[i])
                {
                    sb.AppendLine($"  {move}");
                }
            }

            return sb.ToString();
        }
    }
}