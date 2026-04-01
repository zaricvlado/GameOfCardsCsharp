using System.ComponentModel;

namespace GameOfCardsCsharp.PreferanceBots.Models
{
    /// <summary>
    /// View model for displaying cards in Preferance game
    /// </summary>
    public class PreferanceCardViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public Card Card { get; set; } = null!;
        public string DisplayText { get; set; } = string.Empty;
        public string TextColor { get; set; } = "Black";
        public string BackgroundColor { get; set; } = "White";

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(BorderColor));
                    OnPropertyChanged(nameof(BorderThickness));
                }
            }
        }

        public string BorderColor => IsSelected ? "#FFD700" : "#757575";
        public int BorderThickness => IsSelected ? 4 : 2;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Creates a card view model from a Card
        /// </summary>
        public static PreferanceCardViewModel FromCard(Card card, bool showCard = true)
        {
            if (!showCard)
            {
                // Card back
                return new PreferanceCardViewModel
                {
                    Card = card,
                    DisplayText = "🂠",
                    TextColor = "#444",
                    BackgroundColor = "#DDD"
                };
            }

            // Card face
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
                ? "#DC143C"
                : "#000";

            return new PreferanceCardViewModel
            {
                Card = card,
                DisplayText = $"{rankText}\n{suitSymbol}",
                TextColor = color,
                BackgroundColor = "White"
            };
        }
    }
}
