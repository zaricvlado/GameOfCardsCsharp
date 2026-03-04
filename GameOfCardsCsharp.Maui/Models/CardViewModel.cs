namespace GameOfCardsCsharp.Maui.Models
{
    public class CardViewModel
    {
        public string Suit { get; set; } = string.Empty;
        public string Rank { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public int Value { get; set; }

        // Unicode playing card characters for visual display
        public static string GetCardSymbol(string suit, string rank)
        {
            // Map suits to symbols
            var suitSymbol = suit switch
            {
                "Hearts" => "♥",
                "Diamonds" => "♦",
                "Clubs" => "♣",
                "Spades" => "♠",
                _ => ""
            };

            // Map rank to display value
            var rankDisplay = rank switch
            {
                "Ace" => "A",
                "Jack" => "J",
                "Queen" => "Q",
                "King" => "K",
                _ => rank
            };

            // Use non-breaking space to prevent line breaks between rank and suit
            return $"{rankDisplay}\u00A0{suitSymbol}";
        }
    }
}