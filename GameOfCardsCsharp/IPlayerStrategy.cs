using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp
{
    /// <summary>
    /// AI strategy interface
    /// </summary>
    public interface IPlayerStrategy
    {
        PlayerMove DecideMove(TablicGameState state, List<PossibleMove> possibleMoves);
        
        /// <summary>
        /// Evaluate the score/quality of a potential move
        /// Higher score indicates a better move
        /// </summary>
        int EvaluateMove(PossibleMove move, TablicGameState state);
        
        string GetStrategyName();
    }
}