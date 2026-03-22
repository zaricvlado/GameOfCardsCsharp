using System.Collections.Generic;
using System.Linq;

namespace GameOfCardsCsharp.Preferance.Common
{
    /// <summary>
    /// Suit control information for a single suit
    /// </summary>
    public class SuitControl
    {
        public int MyStoppers { get; set; }
        public int OppStoppers { get; set; }
        public Player Controller { get; set; }
        public int MySuitLength { get; set; }
        public int OppSuitLength { get; set; }
    }

    /// <summary>
    /// Complete suit control analysis across all suits
    /// </summary>
    public class SuitControlReport
    {
        public Dictionary<Suit, SuitControl> SuitControls { get; set; } = new();

        public int MySuitsControlled => SuitControls.Count(kvp => kvp.Value.Controller == Player.Me);

        public int OppSuitsControlled => SuitControls.Count(kvp => kvp.Value.Controller == Player.Opponent);

        public bool IHaveDominance => MySuitsControlled > OppSuitsControlled;

        public int TotalMyStoppers => SuitControls.Values.Sum(s => s.MyStoppers);

        public int TotalOppStoppers => SuitControls.Values.Sum(s => s.OppStoppers);
    }
}
