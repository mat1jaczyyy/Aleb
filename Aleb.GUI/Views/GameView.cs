﻿using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;

using Aleb.Client;
using Aleb.Common;
using Aleb.GUI.Components;
using Aleb.GUI.Prompts;

namespace Aleb.GUI.Views {
    public class GameView: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            UserText = this.Get<DockPanel>("Root").Children.OfType<UserInRoom>().ToList();
            Cards = this.Get<StackPanel>("Cards");

            prompt = this.Get<Border>("Prompt");
        }

        List<UserInRoom> UserText;
        StackPanel Cards;

        Border prompt;

        public Control Prompt {
            get => (Control)prompt.Child;
            set {
                prompt.Child = value;
                prompt.IsVisible = Prompt != null;
            }
        }

        public GameView() => throw new InvalidOperationException();

        public GameView(List<string> names) {
            InitializeComponent();
            
            UserText.Swap(1, 2);
            names.Swap(1, 2);

            foreach (var (text, name) in UserText.Zip(names.RotateWith(i => i == App.User.Name)))
                text.Text = name;

            UserText = UserText.RotateWith(i => i.Text == names[0]).ToList();

            You = UserText.IndexOf(UserText.First(i => i.Text == App.User.Name));
        }

        void Loaded(object sender, VisualTreeAttachmentEventArgs e) {
            Network.GameStarted += GameStarted;
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Network.GameStarted -= GameStarted;
        }

        GameState State;
        int You;

        void SetPlaying(int playing) {
            for (int i = 0; i < 4; i++)
                UserText[i].Background = i == playing
                    ? (IBrush)Application.Current.FindResource("ThemeForegroundLowBrush")
                    : new SolidColorBrush(Color.Parse("Transparent"));

            if (playing != You) return;

            if (State == GameState.Bidding)
                Prompt = new BidPrompt();
        }

        public void GameStarted(int dealer, List<int> cards) {
            for (int i = 0; i < 4; i++)
                UserText[i].Ready.State = i == dealer;

            State = GameState.Bidding;

            SetPlaying(Utilities.Modulo(dealer + 1, 4));

            Cards.Children.Clear();

            foreach (int card in cards)
                Cards.Children.Add(new CardImage(card));

            Cards.Children.Add(new CardImage(32));
            Cards.Children.Add(new CardImage(32));
        }
    }
}
