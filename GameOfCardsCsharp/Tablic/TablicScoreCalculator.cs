using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameOfCardsCsharp.Tablic
{
    public class PlayerScore
    {
        public int Cards { get; set; }
        public int Tricks { get; set; }
        public int TalonPoints { get; set; }
        public int TotalPoints { get; set; }
    }

    public class TablicScoreCalculator
    {
        private readonly GameRules rules;

        public TablicScoreCalculator(GameRules rules)
        {
            this.rules = rules;
        }

        public (PlayerScore, PlayerScore) CalculateFinalScores(TablicPlayer player1, TablicPlayer player2)
        {
            var score1 = CalculatePlayerScore(player1);
            var score2 = CalculatePlayerScore(player2);

            // Award bonus point for most cards (27+ cards in a 52-card deck)
            if (player1.GetPileSize() > player2.GetPileSize())
            {
                score1.TotalPoints++;
            }
            else if (player2.GetPileSize() > player1.GetPileSize())
            {
                score2.TotalPoints++;
            }

            return (score1, score2);
        }

        private PlayerScore CalculatePlayerScore(TablicPlayer player)
        {
            var pile = player.GetPile();
            int tricks = pile.Sum(card => rules.GetTrickValue(card));
            int talonPoints = player.GetTalonClearCount();

            return new PlayerScore
            {
                Cards = pile.Count,
                Tricks = tricks,
                TalonPoints = talonPoints,
                TotalPoints = tricks + talonPoints
            };
        }

        public int DetermineWinner(PlayerScore score1, PlayerScore score2)
        {
            if (score1.TotalPoints > score2.TotalPoints)
                return 0;
            if (score2.TotalPoints > score1.TotalPoints)
                return 1;
            return -1; // Tie
        }
    }
}