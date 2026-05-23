using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Tablic
{
    /// <summary>
    /// Player move
    /// </summary>
    public class PlayerMove
    {
        public int PlayerId { get; set; }
        public int HandIndex { get; set; }
        public bool AttemptPickup { get; set; }
        public List<int> TalonIndices { get; set; } = new();
    }

    /// <summary>
    /// Possible move (useful for UI/helpers)
    /// </summary>
    public class PossibleMove
    {
        public int HandIndex { get; set; }
        public Card Card { get; set; } = null!;
        public bool CanPickup { get; set; }
        public List<List<int>> PossibleCombinations { get; set; } = new();
    }

    /// <summary>
    /// Player type
    /// </summary>
    public enum PlayerType
    {
        Human,
        AI,
        Network
    }

    /// <summary>
    /// Player interface
    /// </summary>
    public interface IPlayer
    {
        int GetPlayerId();
        PlayerType GetPlayerType();
        string GetPlayerName();

        /// <summary>
        /// Returns move if computed (AI), null if needs external input (Human)
        /// </summary>
        PlayerMove? RequestMove(TablicGameEngine engine);

        // Event notifications (optional)
        void OnGameStarted() { }
        void OnRoundStarted(int roundNumber) { }
        void OnCardPlayed(int playerId, Card card, bool wasPickup) { }
        void OnGameFinished(int winnerId) { }
    }
}