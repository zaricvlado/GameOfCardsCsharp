using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Player identifier in suit control analysis
    /// </summary>
    public enum Player
    {
        Me,
        Opponent,
        Contested
    }

    /// <summary>
    /// Analysis mode for Sans2TrickAnalyzer
    /// </summary>
    public enum AnalysisMode
    {
        /// <summary>
        /// All cards are known (for testing/debugging)
        /// </summary>
        PerfectInformation,

        /// <summary>
        /// Opponent cards are unknown (real gameplay with Monte Carlo)
        /// </summary>
        MonteCarloSimulation
    }

    /// <summary>
    /// Game mode in Preferance
    /// </summary>
    public enum PreferanceGameMode
    {
        Sans,   // No trump, maximize tricks
        Trump,  // One suit is trump
        Betl    // Misère - avoid taking tricks
    }
}
