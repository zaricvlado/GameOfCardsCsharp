using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Represents a single card move in perfect information analysis.
    /// Cards from all players are merged and organized by suit.
    /// </summary>
    public class PerfectCardMove
    {
        /// <summary>
        /// The card being played
        /// </summary>
        public Card Card { get; }

        /// <summary>
        /// Index of the player who owns this card (from PerfPerfectGameState.Players)
        /// </summary>
        public int PlayerIndex { get; }

        /// <summary>
        /// Index within the suit-specific list (0-based position in the sorted suit list)
        /// </summary>
        public int ListIndex { get; set; }

        /// <summary>
        /// Whether this card is currently available to play (can be modified)
        /// </summary>
        public bool Available { get; set; }

        /// <summary>
        /// Expected score for each player if this move is played (only set for evaluated moves).
        /// Null if this move hasn't been evaluated yet.
        /// Index corresponds to player index in PerfPerfectGameState.Players.
        /// </summary>
        public int[]? ExpectedTricks { get; set; }

        public PerfectCardMove(Card card, int playerIndex, int listIndex, bool available, int[]? expectedTricks = null)
        {
            Card = card;
            PlayerIndex = playerIndex;
            ListIndex = listIndex;
            Available = available;
            ExpectedTricks = expectedTricks;
        }

        /// <summary>
        /// Creates a copy of this move with the expected tricks set
        /// </summary>
        public PerfectCardMove WithExpectedTricks(int[] expectedTricks)
        {
            return new PerfectCardMove(Card, PlayerIndex, ListIndex, Available, expectedTricks);
        }

        /// <summary>
        /// Gets the expected tricks for a specific player
        /// </summary>
        public int? GetExpectedTricksForPlayer(int playerIndex)
        {
            if (ExpectedTricks == null || playerIndex < 0 || playerIndex >= ExpectedTricks.Length)
                return null;
            
            return ExpectedTricks[playerIndex];
        }

        public override string ToString()
        {
            var tricksInfo = ExpectedTricks != null 
                ? $", Expected: [{string.Join(",", ExpectedTricks)}]" 
                : "";
            
            return $"{Card} (P{PlayerIndex}, Idx:{ListIndex}, {(Available ? "Available" : "Played")}{tricksInfo})";
        }
    }
}
