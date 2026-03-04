using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class GameRulesTests
    {
        [Fact]
        public void StandardRules_AceHighest()
        {
            var rules = new StandardRules();
            Assert.Equal(14, rules.GetRankValue(Rank.Ace));
            Assert.Equal(13, rules.GetRankValue(Rank.King));
        }

        [Fact]
        public void AceLowRules_AceLowest()
        {
            var rules = new AceLowRules();
            Assert.Equal(1, rules.GetRankValue(Rank.Ace));
            Assert.Equal(2, rules.GetRankValue(Rank.Two));
        }

        [Fact]
        public void BlackjackRules_FaceCardsAre10()
        {
            var rules = new BlackjackRules();
            var jack = new Card(Rank.Jack, Suit.Spades);
            var queen = new Card(Rank.Queen, Suit.Hearts);
            var king = new Card(Rank.King, Suit.Diamonds);

            Assert.Equal(10, rules.GetCardValue(jack));
            Assert.Equal(10, rules.GetCardValue(queen));
            Assert.Equal(10, rules.GetCardValue(king));
        }

        [Fact]
        public void BlackjackRules_AceIs11()
        {
            var rules = new BlackjackRules();
            var ace = new Card(Rank.Ace, Suit.Spades);
            Assert.Equal(11, rules.GetCardValue(ace));
        }

        [Fact]
        public void TablicRules_TrickValues()
        {
            var rules = new TablicRules();

            var twoClubs = new Card(Rank.Two, Suit.Clubs);
            var tenDiamonds = new Card(Rank.Ten, Suit.Diamonds);
            var aceSpades = new Card(Rank.Ace, Suit.Spades);
            var fiveHearts = new Card(Rank.Five, Suit.Hearts);

            Assert.Equal(1, rules.GetTrickValue(twoClubs));
            Assert.Equal(2, rules.GetTrickValue(tenDiamonds));
            Assert.Equal(1, rules.GetTrickValue(aceSpades));
            Assert.Equal(0, rules.GetTrickValue(fiveHearts));
        }
    }
}