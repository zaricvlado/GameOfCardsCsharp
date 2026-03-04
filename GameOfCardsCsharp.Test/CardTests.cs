using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class CardTests
    {
        [Fact]
        public void Card_ToString_ReturnsCorrectFormat()
        {
            var card = new Card(Rank.Ace, Suit.Spades);
            string str = card.ToString();

            Assert.Contains("Ace", str);
            Assert.Contains("Spades", str);
        }

        [Fact]
        public void Card_GetRank_ReturnsCorrectRank()
        {
            var card = new Card(Rank.King, Suit.Hearts);
            Assert.Equal(Rank.King, card.Rank);
        }

        [Fact]
        public void Card_GetSuit_ReturnsCorrectSuit()
        {
            var card = new Card(Rank.Queen, Suit.Diamonds);
            Assert.Equal(Suit.Diamonds, card.Suit);
        }
    }
}