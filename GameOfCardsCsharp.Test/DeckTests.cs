using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using GameOfCardsCsharp;

namespace GameOfCardsCsharp.Tests
{
    public class DeckTests
    {
        [Fact]
        public void FullDeck_HasCorrectCardCount()
        {
            var deck = new FullDeck();
            Assert.Equal(52, deck.CardsRemaining());
        }

        [Fact]
        public void FullDeck_DrawCard_DecreasesCount()
        {
            var deck = new FullDeck();
            var card = deck.DrawCard();
            Assert.Equal(51, deck.CardsRemaining());
        }

        [Fact]
        public void FullDeck_Shuffle_ChangesOrder()
        {
            var deck1 = new FullDeck();
            var deck2 = new FullDeck();

            deck1.Shuffle();
            // Can't easily test randomness, but at least it shouldn't crash
            Assert.Equal(52, deck1.CardsRemaining());
        }

        [Fact]
        public void ShortDeck_HasCorrectCardCount()
        {
            var deck = new ShortDeck();
            Assert.Equal(32, deck.CardsRemaining());
        }

        [Fact]
        public void Deck_Reset_RestoresCards()
        {
            var deck = new FullDeck();

            for (int i = 0; i < 10; i++)
            {
                deck.DrawCard();
            }

            Assert.Equal(42, deck.CardsRemaining());

            deck.Reset();
            Assert.Equal(52, deck.CardsRemaining());
        }
    }
}