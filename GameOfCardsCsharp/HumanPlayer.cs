using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// Human player
    /// </summary>
    public class HumanPlayer : IPlayer
    {
        private readonly int playerId;
        private readonly string playerName;

        public HumanPlayer(int playerId, string name = "Player")
        {
            this.playerId = playerId;
            playerName = name;
        }

        public int GetPlayerId() => playerId;

        public PlayerType GetPlayerType() => PlayerType.Human;

        public string GetPlayerName() => playerName;

        /// <summary>
        /// Returns null - human needs external input
        /// </summary>
        public PlayerMove? RequestMove(TablicGameEngine engine)
        {
            return null;
        }
    }
}