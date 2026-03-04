using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class HandTests
    {
        [Fact]
        public void Hand_AddCard_IncreasesCount()
        {
            var hand = new Hand();
            var card = new Card(Rank.Ace, Suit.Spades);

            hand.AddCard(card);
            Assert.Equal(1, hand.CardCount());
        }

        [Fact]
        public void Hand_Clear_RemovesAllCards()
        {
            var hand = new Hand();
            hand.AddCard(new Card(Rank.Ace, Suit.Spades));
            hand.AddCard(new Card(Rank.King, Suit.Hearts));

            Assert.Equal(2, hand.CardCount());

            hand.Clear();
            Assert.Equal(0, hand.CardCount());
        }

        [Fact]
        public void Hand_CalculateTotal_StandardRules()
        {
            var hand = new Hand();
            hand.AddCard(new Card(Rank.Ace, Suit.Spades));   // 14
            hand.AddCard(new Card(Rank.King, Suit.Hearts));  // 13

            var rules = new StandardRules();
            int total = hand.CalculateTotal(rules);

            Assert.Equal(27, total);
        }

        [Fact]
        public void Hand_CalculateTricks_TablicRules()
        {
            var hand = new Hand();
            hand.AddCard(new Card(Rank.Two, Suit.Clubs));    // 1 trick
            hand.AddCard(new Card(Rank.Ten, Suit.Diamonds)); // 2 tricks (special)
            hand.AddCard(new Card(Rank.Ace, Suit.Spades));   // 1 trick

            var rules = new TablicRules();
            int tricks = hand.CalculateTricks(rules);

            Assert.Equal(4, tricks);
        }
    }
}