using GameOfCardsCsharp.Maui.Models;
using System.Collections.ObjectModel;

namespace GameOfCardsCsharp.Maui
{
    public partial class MainPage : ContentPage
    {
        private ObservableCollection<CardViewModel> playerHand;
        private ObservableCollection<CardViewModel> opponentHand;
        private CardViewModel? selectedCard;
        private Border? selectedBorder;  // Track the selected card's border
        private int playerScore = 0;
        private int opponentScore = 0;
        private bool isProcessingCard = false;

        public MainPage()
        {
            InitializeComponent();
            playerHand = new ObservableCollection<CardViewModel>();
            opponentHand = new ObservableCollection<CardViewModel>();
            
            PlayerHandCollection.ItemsSource = playerHand;
            OpponentHandCollection.ItemsSource = opponentHand;
        }

        private void OnNewGameClicked(object sender, EventArgs e)
        {
            // Reset game state
            playerHand.Clear();
            opponentHand.Clear();
            playerScore = 0;
            opponentScore = 0;
            selectedCard = null;
            selectedBorder = null;
            isProcessingCard = false;

            // Sample: Deal 5 cards to each player
            var suits = new[] { "Hearts", "Diamonds", "Clubs", "Spades" };
            var ranks = new[] { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };
            var random = new Random();

            for (int i = 0; i < 5; i++)
            {
                var suit = suits[random.Next(suits.Length)];
                var rank = ranks[random.Next(ranks.Length)];
                
                playerHand.Add(new CardViewModel
                {
                    Suit = suit,
                    Rank = rank,
                    DisplayText = CardViewModel.GetCardSymbol(suit, rank),
                    Value = Array.IndexOf(ranks, rank) + 2
                });

                // Opponent gets cards (shown as count only)
                opponentHand.Add(new CardViewModel());
            }

            UpdateScoreDisplay();
            GameStatusLabel.Text = "Tap a card to select it";
            PlayerCardBorder.IsVisible = false;
            OpponentCardBorder.IsVisible = false;
        }

        private async void OnCardTapped(object sender, EventArgs e)
        {
            if (isProcessingCard)
                return;

            if (sender is not Border tappedBorder || tappedBorder.BindingContext is not CardViewModel card)
                return;

            // If this card is already selected, play it (second tap = confirm)
            if (selectedCard == card && selectedBorder == tappedBorder)
            {
                GameStatusLabel.Text = $"Playing {card.DisplayText}...";
                await PlayCard();
            }
            else
            {
                // First tap - select the card
                
                // Deselect previous card
                if (selectedBorder != null)
                {
                    selectedBorder.Stroke = Color.FromArgb("#757575");
                    selectedBorder.StrokeThickness = 2;
                    selectedBorder.Scale = 1.0;
                }

                // Select new card
                selectedCard = card;
                selectedBorder = tappedBorder;

                // Visual feedback: highlight selected card
                selectedBorder.Stroke = Colors.Gold;
                selectedBorder.StrokeThickness = 4;
                selectedBorder.Scale = 1.1;  // Slightly enlarge

                GameStatusLabel.Text = $"Selected: {card.DisplayText} - Tap again to play, or tap another card";
            }
        }

        private async Task PlayCard()
        {
            if (selectedCard == null || playerHand.Count == 0 || isProcessingCard)
                return;

            isProcessingCard = true;

            // Reset selected card visual
            if (selectedBorder != null)
            {
                selectedBorder.Stroke = Color.FromArgb("#757575");
                selectedBorder.StrokeThickness = 2;
                selectedBorder.Scale = 1.0;
            }

            // Show player's card
            PlayerCardLabel.Text = selectedCard.DisplayText;
            PlayerCardBorder.IsVisible = true;

            // Simulate opponent playing a card
            var opponentCard = opponentHand[0];
            var suits = new[] { "Hearts", "Diamonds", "Clubs", "Spades" };
            var ranks = new[] { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };
            var random = new Random();
            
            var suit = suits[random.Next(suits.Length)];
            var rank = ranks[random.Next(ranks.Length)];
            opponentCard.Suit = suit;
            opponentCard.Rank = rank;
            opponentCard.DisplayText = CardViewModel.GetCardSymbol(suit, rank);
            opponentCard.Value = Array.IndexOf(ranks, rank) + 2;

            OpponentCardLabel.Text = opponentCard.DisplayText;
            OpponentCardBorder.IsVisible = true;

            await Task.Delay(500);

            // Determine winner
            if (selectedCard.Value > opponentCard.Value)
            {
                playerScore++;
                GameStatusLabel.Text = "You win this round! 🎉";
            }
            else if (selectedCard.Value < opponentCard.Value)
            {
                opponentScore++;
                GameStatusLabel.Text = "Opponent wins this round";
            }
            else
            {
                GameStatusLabel.Text = "It's a tie!";
            }

            // Remove played cards
            playerHand.Remove(selectedCard);
            opponentHand.Remove(opponentCard);
            selectedCard = null;
            selectedBorder = null;

            UpdateScoreDisplay();

            await Task.Delay(1500);

            // Check if game is over
            if (playerHand.Count == 0)
            {
                var winner = playerScore > opponentScore ? "You win the game! 🏆" :
                           playerScore < opponentScore ? "Opponent wins the game!" :
                           "Game is a tie!";
                
                await DisplayAlert("Game Over", winner, "OK");
                GameStatusLabel.Text = "Press 'New Game' to play again";
                PlayerCardBorder.IsVisible = false;
                OpponentCardBorder.IsVisible = false;
            }
            else
            {
                GameStatusLabel.Text = "Tap a card to select it";
                PlayerCardBorder.IsVisible = false;
                OpponentCardBorder.IsVisible = false;
            }

            isProcessingCard = false;
        }

        private void UpdateScoreDisplay()
        {
            PlayerScoreLabel.Text = $"You: {playerScore}";
            OpponentScoreLabel.Text = $"Opponent: {opponentScore}";
        }
    }
}
