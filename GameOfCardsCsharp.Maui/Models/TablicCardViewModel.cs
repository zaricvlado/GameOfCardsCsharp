using System;
using System.ComponentModel;

namespace GameOfCardsCsharp.Maui.Models
{
    public class TablicCardViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        public Card Card { get; set; } = null!;
        public int Index { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        public string SuitColor { get; set; } = "Black";

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

        public static string GetCardSymbol(Card card)
        {
            var suitSymbol = card.Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => ""
            };

            var rankDisplay = card.Rank switch
            {
                Rank.Two => "2",
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
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

            return $"{rankDisplay}\u00A0{suitSymbol}";
        }

        public static string GetSuitColor(Card card)
        {
            return card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds ? "#DC143C" : "#000000";
        }
    }
}
