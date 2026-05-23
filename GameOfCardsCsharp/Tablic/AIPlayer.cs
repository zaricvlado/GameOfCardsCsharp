using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Tablic
{
    /// <summary>
    /// AI player
    /// </summary>
    public class AIPlayer : IPlayer
    {
        private readonly int playerId;
        private string playerName;
        private IPlayerStrategy strategy;

        public AIPlayer(int playerId, IPlayerStrategy strategy, string name = "AI")
        {
            this.playerId = playerId;
            this.strategy = strategy;
            playerName = $"{name} ({strategy.GetStrategyName()})";
        }

        public int GetPlayerId() => playerId;

        public PlayerType GetPlayerType() => PlayerType.AI;

        public string GetPlayerName() => playerName;

        /// <summary>
        /// Returns computed move immediately
        /// </summary>
        public PlayerMove? RequestMove(TablicGameEngine engine)
        {
            var possibleMoves = engine.GetPossibleMoves();
            var decision = strategy.DecideMove(engine.GetState(), possibleMoves);

            return new PlayerMove
            {
                PlayerId = playerId,
                HandIndex = decision.HandIndex,
                AttemptPickup = decision.AttemptPickup,
                TalonIndices = decision.TalonIndices
            };
        }

        public void SetStrategy(IPlayerStrategy newStrategy)
        {
            strategy = newStrategy;
            playerName = $"AI ({strategy.GetStrategyName()})";
        }

        public IPlayerStrategy GetStrategy() => strategy;
    }
}