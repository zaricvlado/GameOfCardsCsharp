using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.GamePlay
{
    /// <summary>
    /// Result of a completed game play phase
    /// </summary>
    public class GamePlayResult
    {
        public ContractType Contract { get; }
        public bool DeclarerWon { get; }
        public int TricksWonByDeclarer { get; }
        public int TricksWonByDefenders { get; }
        
        /// <summary>
        /// Actual tricks won by each player (for display purposes)
        /// </summary>
        public Dictionary<int, int> ActualTricksByPlayer { get; }
        
        /// <summary>
        /// Attributed tricks for scoring (Partner tricks → Defender)
        /// </summary>
        public Dictionary<int, int> AttributedTricksByPlayer { get; }

        public GamePlayResult(
            ContractType contract,
            bool declarerWon,
            int tricksWonByDeclarer,
            int tricksWonByDefenders,
            Dictionary<int, int> actualTricksByPlayer,
            Dictionary<int, int> attributedTricksByPlayer)
        {
            Contract = contract;
            DeclarerWon = declarerWon;
            TricksWonByDeclarer = tricksWonByDeclarer;
            TricksWonByDefenders = tricksWonByDefenders;
            ActualTricksByPlayer = actualTricksByPlayer;
            AttributedTricksByPlayer = attributedTricksByPlayer;
        }

        /// <summary>
        /// Gets a detailed breakdown showing Partner trick attribution
        /// </summary>
        public string GetTrickBreakdown()
        {
            var lines = new List<string>();
            
            lines.Add("Actual tricks won:");
            foreach (var kvp in ActualTricksByPlayer.OrderBy(x => x.Key))
            {
                lines.Add($"  Player {kvp.Key}: {kvp.Value} tricks");
            }

            // Check if attribution differs from actual
            bool hasAttribution = AttributedTricksByPlayer.Any(kvp => 
                ActualTricksByPlayer[kvp.Key] != kvp.Value);

            if (hasAttribution)
            {
                lines.Add("");
                lines.Add("Attributed tricks (for scoring):");
                foreach (var kvp in AttributedTricksByPlayer.OrderBy(x => x.Key))
                {
                    var actual = ActualTricksByPlayer[kvp.Key];
                    if (actual != kvp.Value)
                    {
                        lines.Add($"  Player {kvp.Key}: {kvp.Value} tricks (actual: {actual}, includes partner)");
                    }
                    else
                    {
                        lines.Add($"  Player {kvp.Key}: {kvp.Value} tricks");
                    }
                }
            }

            return string.Join("\n", lines);
        }

        public override string ToString()
        {
            var result = DeclarerWon ? "WON" : "LOST";
            return $"Game Result: Declarer {result} {Contract}\n" +
                   $"Tricks: Declarer {TricksWonByDeclarer}, Defenders {TricksWonByDefenders}\n" +
                   GetTrickBreakdown();
        }
    }
}
