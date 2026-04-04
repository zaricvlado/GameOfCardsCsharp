using GameOfCardsCsharp.Preferance;
using Microsoft.Maui.Graphics;

namespace GameOfCardsCsharp.PreferanceBots.Models
{
    /// <summary>
    /// View model for displaying cards in Preferance game
    /// </summary>
    public class PreferanceCardViewModel
    {
        public Card Card { get; set; }
        public string DisplayText { get; set; } = "";
        public string RankText { get; set; } = "";
        public string SuitSymbol { get; set; } = "";
        public Color TextColor { get; set; } = Colors.Black;
        public Color BackgroundColor { get; set; } = Colors.White;
        public Color BorderColor { get; set; } = Color.FromArgb("#888");
        public int BorderThickness { get; set; } = 2;

        /// <summary>
        /// Creates a card view model from a Card
        /// </summary>
        public static PreferanceCardViewModel FromCard(Card card, bool showCard)
        {
            var viewModel = new PreferanceCardViewModel
            {
                Card = card
            };

            if (!showCard)
            {
                // Hidden card (back of card)
                viewModel.DisplayText = "🂠";
                viewModel.RankText = "";
                viewModel.SuitSymbol = "";
                viewModel.TextColor = Color.FromArgb("#444");
                viewModel.BackgroundColor = Color.FromArgb("#DDD");
                viewModel.BorderColor = Color.FromArgb("#888");
            }
            else
            {
                // Visible card (front of card)
                var suitSymbol = card.Suit switch
                {
                    Suit.Hearts => "♥",
                    Suit.Diamonds => "♦",
                    Suit.Clubs => "♣",
                    Suit.Spades => "♠",
                    _ => ""
                };

                var rankText = card.Rank switch
                {
                    Rank.Seven => "7",
                    Rank.Eight => "8",
                    Rank.Nine => "9",
                    Rank.Ten => "10",
                    Rank.Jack => "J",
                    Rank.Queen => "Q",
                    Rank.King => "K",
                    Rank.Ace => "A",
                    _ => "?"
                };

                var color = (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds)
                    ? Color.FromArgb("#DC143C")
                    : Colors.Black;

                viewModel.DisplayText = $"{rankText}\n{suitSymbol}";
                viewModel.RankText = rankText;
                viewModel.SuitSymbol = suitSymbol;
                viewModel.TextColor = color;
                viewModel.BackgroundColor = Colors.White;
                viewModel.BorderColor = Color.FromArgb("#333");
            }

            return viewModel;
        }
    }
}
