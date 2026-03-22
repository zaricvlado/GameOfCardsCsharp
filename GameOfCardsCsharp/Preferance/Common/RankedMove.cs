using System.Collections.Generic;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Represents an analyzed move with its evaluation (shared across all game modes)
    /// </summary>
    public class RankedMove
    {
        public Card Card { get; set; } = null!;
        
        public double ExpectedTricks { get; set; }
        
        /// <summary>
        /// Predicted number of tricks for "my" player after this move
        /// </summary>
        public double PredictedMyTricks { get; set; }
        
        /// <summary>
        /// Predicted number of tricks for opponent after this move
        /// </summary>
        public double PredictedOpponentTricks { get; set; }
        
        public double Confidence { get; set; }
        
        public double MinScore { get; set; }
        
        public double MaxScore { get; set; }
        
        public double StandardDeviation { get; set; }
        
        public List<AlternativeMove> Alternatives { get; set; } = new();
        
        public string Reasoning { get; set; } = string.Empty;
    }

    /// <summary>
    /// Alternative move option
    /// </summary>
    public class AlternativeMove
    {
        public Card Card { get; set; } = null!;
        public double ExpectedTricks { get; set; }
        public double PredictedMyTricks { get; set; }
        public double PredictedOpponentTricks { get; set; }
        public double Confidence { get; set; }
    }
}
