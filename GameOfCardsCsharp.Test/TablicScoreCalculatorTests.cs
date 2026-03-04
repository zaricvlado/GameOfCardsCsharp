using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class TablicScoreCalculatorTests
    {
        [Fact]
        public void TablicScoreCalculator_CalculateFinalScores_NoCards()
        {
            var rules = new TablicRules();
            var calculator = new TablicScoreCalculator(rules);
            var player1 = new TablicPlayer(0);
            var player2 = new TablicPlayer(1);

            var (score1, score2) = calculator.CalculateFinalScores(player1, player2);

            Assert.Equal(0, score1.Tricks);
            Assert.Equal(0, score1.TotalPoints);
        }

        [Fact]
        public void TablicScoreCalculator_CalculateFinalScores_WithTricks()
        {
            var rules = new TablicRules();
            var calculator = new TablicScoreCalculator(rules);
            var player = new TablicPlayer(0);
            var player2 = new TablicPlayer(1);

            player.AddToPile(new Card(Rank.Two, Suit.Clubs));    // 1 trick
            player.AddToPile(new Card(Rank.Ten, Suit.Diamonds)); // 2 tricks
            player.AddToPile(new Card(Rank.Ace, Suit.Spades));   // 1 trick

            var (score1, score2) = calculator.CalculateFinalScores(player, player2);

            Assert.Equal(4, score1.Tricks);
        }

        [Fact]
        public void TablicScoreCalculator_CalculateFinalScores_CardBonus()
        {
            var rules = new TablicRules();
            var calculator = new TablicScoreCalculator(rules);
            var player1 = new TablicPlayer(0);
            var player2 = new TablicPlayer(1);

            // Add 27 cards to player1 (more than half of 52)
            for (int i = 0; i < 27; i++)
            {
                player1.AddToPile(new Card(Rank.Two, Suit.Clubs));
            }

            var (score1, score2) = calculator.CalculateFinalScores(player1, player2);

            // Player with more cards gets bonus point
            Assert.True(score1.TotalPoints > score2.TotalPoints);
        }

        [Fact]
        public void TablicScoreCalculator_DetermineWinner()
        {
            var rules = new TablicRules();
            var calculator = new TablicScoreCalculator(rules);

            var score1 = new PlayerScore { Tricks = 5, TotalPoints = 8 };
            var score2 = new PlayerScore { Tricks = 4, TotalPoints = 4 };

            int winner = calculator.DetermineWinner(score1, score2);

            Assert.Equal(0, winner);
        }

        [Fact]
        public void TablicScoreCalculator_DetermineWinner_Tie()
        {
            var rules = new TablicRules();
            var calculator = new TablicScoreCalculator(rules);

            var score1 = new PlayerScore { Tricks = 5, TotalPoints = 5 };
            var score2 = new PlayerScore { Tricks = 5, TotalPoints = 5 };

            int winner = calculator.DetermineWinner(score1, score2);

            Assert.Equal(-1, winner);
        }
    }
}