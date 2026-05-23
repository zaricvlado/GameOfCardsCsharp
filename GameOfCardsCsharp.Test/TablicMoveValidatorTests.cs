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
    public class TablicMoveValidatorTests
    {
        [Fact]
        public void TablicMoveValidator_ValidateCardPlay_CardInHand()
        {
            var rules = new TablicRules();
            var validator = new TablicMoveValidator(rules);

            var card = new Card(Rank.Ace, Suit.Spades);
            var hand = new List<Card> { card };

            var result = validator.ValidateCardPlay(card, hand);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void TablicMoveValidator_ValidateCardPlay_CardNotInHand()
        {
            var rules = new TablicRules();
            var validator = new TablicMoveValidator(rules);

            var card = new Card(Rank.Ace, Suit.Spades);
            var different = new Card(Rank.King, Suit.Hearts);
            var hand = new List<Card> { different };

            var result = validator.ValidateCardPlay(card, hand);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void TablicMoveValidator_FindMatches_ExactMatch()
        {
            var rules = new TablicRules();
            var validator = new TablicMoveValidator(rules);

            var playedCard = new Card(Rank.Five, Suit.Spades);  // value = 5
            var talon = new List<Card>
            {
                new Card(Rank.Five, Suit.Hearts)                // value = 5
            };

            var matches = validator.FindMatches(playedCard, talon);
            Assert.NotEmpty(matches);
        }

        [Fact]
        public void TablicMoveValidator_FindMatches_SumMatch()
        {
            var rules = new TablicRules();
            var validator = new TablicMoveValidator(rules);

            var playedCard = new Card(Rank.Seven, Suit.Spades);  // value = 7
            var talon = new List<Card>
            {
                new Card(Rank.Three, Suit.Hearts),               // value = 3
                new Card(Rank.Four, Suit.Diamonds)               // value = 4
            };

            var matches = validator.FindMatches(playedCard, talon);
            Assert.NotEmpty(matches);
        }

        [Fact]
        public void TablicMoveValidator_FindMatches_NoMatch()
        {
            var rules = new TablicRules();
            var validator = new TablicMoveValidator(rules);

            var playedCard = new Card(Rank.Ace, Suit.Spades);    // value = 11
            var talon = new List<Card>
            {
                new Card(Rank.Two, Suit.Hearts)                  // value = 2
            };

            var matches = validator.FindMatches(playedCard, talon);
            Assert.Empty(matches);
        }
    }
}