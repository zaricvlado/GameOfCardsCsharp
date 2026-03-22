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

        public PerfectCardMove(Card card, int playerIndex, int listIndex, bool available)
        {
            Card = card;
            PlayerIndex = playerIndex;
            ListIndex = listIndex;
            Available = available;
        }

        public override string ToString()
        {
            return $"{Card} (P{PlayerIndex}, Idx:{ListIndex}, {(Available ? "Available" : "Played")})";
        }
    }
}
