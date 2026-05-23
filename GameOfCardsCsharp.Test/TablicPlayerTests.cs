using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;
using GameOfCardsCsharp.Tablic;

namespace GameOfCardsCsharp.Tests
{
    public class TablicPlayerTests
    {
        [Fact]
        public void TablicPlayer_GetPlayerId()
        {
            var player = new TablicPlayer(5);
            Assert.Equal(5, player.GetPlayerId());
        }

        [Fact]
        public void TablicPlayer_AddToPile_SingleCard()
        {
            var player = new TablicPlayer(0);
            var card = new Card(Rank.Ace, Suit.Spades);

            player.AddToPile(card);
            Assert.Equal(1, player.GetPileSize());
        }

        [Fact]
        public void TablicPlayer_AddToPile_MultipleCards()
        {
            var player = new TablicPlayer(0);
            var cards = new List<Card>
            {
                new Card(Rank.Ace, Suit.Spades),
                new Card(Rank.King, Suit.Hearts),
                new Card(Rank.Queen, Suit.Diamonds)
            };

            player.AddToPile(cards);
            Assert.Equal(3, player.GetPileSize());
        }

        [Fact]
        public void TablicPlayer_Reset_ClearsHandAndPile()
        {
            var player = new TablicPlayer(0);
            player.GetHand().AddCard(new Card(Rank.Ace, Suit.Spades));
            player.AddToPile(new Card(Rank.King, Suit.Hearts));

            player.Reset();

            Assert.Equal(0, player.GetHand().CardCount());
            Assert.Equal(0, player.GetPileSize());
        }
    }
}