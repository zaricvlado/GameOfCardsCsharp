using Microsoft.Maui.Graphics;

namespace GameOfCardsCsharp.PreferanceBots.Models
{
    /// <summary>
    /// View model for displaying cards in the UI
    /// </summary>
    public class CardViewModel
    {
        public string Suit { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public Color BorderColor { get; set; } = Colors.Gray;
        public int BorderThickness { get; set; } = 2;
        public Color BackgroundColor { get; set; } = Colors.White;
        public Color TextColor { get; set; } = Colors.Black;

        /// <summary>
        /// Gets the card symbol for display (e.g., "A ♠")
        /// </summary>
        public static string GetCardSymbol(Card card)
        {
            var suitSymbol = card.Suit switch
            {
                GameOfCardsCsharp.Suit.Hearts => "♥",
                GameOfCardsCsharp.Suit.Diamonds => "♦",
                GameOfCardsCsharp.Suit.Clubs => "♣",
                GameOfCardsCsharp.Suit.Spades => "♠",
                _ => ""
            };

            var rankDisplay = card.Rank switch
            {
                GameOfCardsCsharp.Rank.Ace => "A",
                GameOfCardsCsharp.Rank.Jack => "J",
                GameOfCardsCsharp.Rank.Queen => "Q",
                GameOfCardsCsharp.Rank.King => "K",
                _ => ((int)card.Rank + 2).ToString()
            };

            // Use non-breaking space to prevent line breaks between rank and suit
            return $"{rankDisplay}\u00A0{suitSymbol}";
        }

        /// <summary>
        /// Gets color for suit (Red for Hearts/Diamonds, Black for Clubs/Spades)
        /// </summary>
        public static Color GetSuitColor(GameOfCardsCsharp.Suit suit)
        {
            return suit == GameOfCardsCsharp.Suit.Hearts || suit == GameOfCardsCsharp.Suit.Diamonds
                ? Colors.Red
                : Colors.Black;
        }
    }
}
