﻿using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

using Aleb.Client;
using Aleb.Common;
using Aleb.GUI.Components;
using Aleb.GUI.Prompts;
using System.Diagnostics;

namespace Aleb.GUI.Views {
    public class GameView: UserControl, ISpectateable {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            UserText = this.Get<DockPanel>("Root").Children.OfType<UserInGame>().ToList();
            GameGrid = this.Get<Grid>("GameGrid");

            YourCardsHolder = this.Get<Border>("YourCardsHolder");
            SpecCardHolders = this.Get<DockPanel>("SpecCardHolders").Children.OfType<SpecCardHolder>().ToList();

            Cards = this.Get<StackPanel>("Cards");
            Rounds = this.Get<StackPanel>("Rounds");
            Score = this.Get<StackPanel>("Score");

            TitleRow = this.Get<RoundRow>("TitleRow");
            Declarations = this.Get<RoundRow>("Declarations");
            CurrentRound = this.Get<RoundRow>("CurrentRound");
            TotalRound = this.Get<RoundRow>("TotalRound");
            Total = this.Get<RoundRow>("Total");
            
            TableSegments = Enumerable.Range(0, 4).Select(i => this.Get<Border>($"Table{i}")).ToList();
            CardTableSegments = Enumerable.Range(0, 4).Select(i => this.Get<Border>($"CardTable{i}")).ToList();

            alert = this.Get<StackPanel>("Alert");
            prompt = this.Get<Border>("Prompt");
            trump = this.Get<Border>("Trump");

            FinalCard = this.Get<DockPanel>("FinalCard").Children.OfType<TextOverlay>().ToList();

            CardsWonRoot = this.Get<StackPanel>("CardsWonRoot");
            CardsWonButton = this.Get<CardsWonIcon>("CardsWonButton");
            CardsWon = this.Get<TextOverlay>("CardsWon");

            HideSpecCardsButton = this.Get<SpectateIcon>("HideSpecCardsButton");

            TimeElapsed = this.Get<TextBlock>("TimeElapsed");
        }

        List<UserInGame> UserText;
        Grid GameGrid;
        
        List<SpecCardHolder> SpecCardHolders;
        Border YourCardsHolder;

        StackPanel Cards, Rounds, Score;

        RoundRow TitleRow, Declarations, CurrentRound, TotalRound, Total;

        List<Border> TableSegments, CardTableSegments;
        
        StackPanel alert;
        Border prompt, trump;

        List<TextOverlay> FinalCard;

        StackPanel CardsWonRoot;
        CardsWonIcon CardsWonButton;
        TextOverlay CardsWon;

        SpectateIcon HideSpecCardsButton;

        TimeSpan startOffset = TimeSpan.Zero;
        Stopwatch timer = new Stopwatch();
        DispatcherTimer Timer;
        TextBlock TimeElapsed;

        void UpdateTime(object sender, EventArgs e) {
            TimeSpan timeElapsed = timer.Elapsed + startOffset;
            TimeElapsed.Text = $"{((int)timeElapsed.TotalHours > 0? $"{(int)timeElapsed.TotalHours}:" : "")}{timeElapsed.Minutes:00}:{timeElapsed.Seconds:00}";
        }

        Control Prompt {
            get => (Control)prompt.Child;
            set {
                if (value is TextOverlay textOverlay) {
                    textOverlay.HorizontalAlignment = HorizontalAlignment.Center;
                    textOverlay.VerticalAlignment = VerticalAlignment.Center;
                }

                prompt.Child = value;
                prompt.Background = (value is TextOverlay)? null : (IBrush)Application.Current.FindResource("ThemeControlMidHighBrush");
                prompt.IsVisible = Prompt != null;
            }
        }

        Control Alert {
            get => (Control)alert.Children.FirstOrDefault();
            set {
                alert.Children.Clear();
                alert.Children.Add(value);
            }
        }

        Trump Trump {
            get => (Trump)trump.Child;
            set => trump.Child = value;
        }

        HorizontalAlignment[] TableH = new HorizontalAlignment[] {
            HorizontalAlignment.Center,
            HorizontalAlignment.Right,
            HorizontalAlignment.Center,
            HorizontalAlignment.Left
        };

        VerticalAlignment[] TableV = new VerticalAlignment[] {
            VerticalAlignment.Bottom,
            VerticalAlignment.Center,
            VerticalAlignment.Top,
            VerticalAlignment.Center
        };

        int Position(int index) => Utilities.Modulo(index - You, 4);

        void Table(int index, Control value) {
            if (value != null) {
                int position = Position(index);
                
                value.HorizontalAlignment = TableH[position];
                value.VerticalAlignment = TableV[position];

                if (value is CardStack cardStack) cardStack.ApplyPosition(position);
            }

            TableSegments[index].Child = value;
        }

        void CardTable(int index, CardImage value) {
            if (value != null) {
                int position = Position(index);
                
                value.HorizontalAlignment = TableH.Rotate(2).ElementAt(position);
                value.VerticalAlignment = TableV.Rotate(2).ElementAt(position);
                
                value.Cursor = Cursor.Default;
                value.MaxHeight = 128;

                CardTableSegments[index].Child = value;

            } else CardTableSegments[index].Child.Opacity = 0;
        }

        void ClearTable() {
            for (int i = 0; i < 4; i++) {
                Table(i, null);
                CardTable(i, null);
            }
        }

        List<int> cardsOnTable = new List<int>();
        List<int> cardsWon = new List<int>();

        void SetCardsWon() {
            if (cardsWon.Count > 0) {
                CardsWonButton.IsVisible = true;
                CardsWon.SetControl(new CardsWonMatrix(cardsWon));

            } else {
                CardsWonButton.IsVisible = CardsWon.IsVisible = false;
                CardsWon.SetControl(null);
            }
        }
        
        void UpdateCardsWon(int winner) {
            if (winner % 2 == Team) {
                cardsWon.AddRange(cardsOnTable);
                SetCardsWon();
            }

            cardsOnTable.Clear();
        }

        void ClearCardsWon() {
            cardsOnTable.Clear();
            cardsWon.Clear();
            
            SetCardsWon();
        }

        CardImage CreateCard(int card) {
            CardImage cardImage = new CardImage(card);
            cardImage.Clicked += CardClicked;
            return cardImage;
        }

        void CreateCards(List<int> cards) {
            Cards.Children.Clear();

            if (App.MainWindow.Spectating) {
                int n = 0;
                
                foreach (var i in cards.GroupBy(x => n++ / 8))
                    SpecCardHolders[i.Key].SetCards(i.ToList());

            } else {
                foreach (int card in cards)
                    Cards.Children.Add(CreateCard(card));

                while (Cards.Children.Count < 8)
                    Cards.Children.Add(CreateCard(32));

                Cards.Parent.Opacity = 1;
            }
        }

        static List<string> FailScore(FinalizedRound score)
            => score.Score.Select(i => (score.Fail && i == 0)? "—" : i.ToString()).ToList();

        static List<string> emptyRow = new List<string>() { "", "" };

        List<int> discScores = new List<int>() { 0, 0 };
        public bool showPts = true;

        void UpdateRow<T>(RoundRow row, List<T> values, bool autoTeams = true) {
            int team = autoTeams? Team : 0;
            row.Left = values[team].ToString();
            row.Right = values[1 - team].ToString();
        }

        void UpdateTitleRow(bool mivi) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => UpdateTitleRow(mivi));
                return;
            }

            UpdateRow(TitleRow, (mivi
                ? new List<string>() { "Mi", "Vi" }
                : new List<string>() { "Vi", "Oni" }
            ), false);
        }

        void UpdateCurrentRound(List<int> calls, List<int> played) {
            UpdateRow(Declarations, calls);
            if (played.Any(i => i != -1)) {
                UpdateRow(CurrentRound, played);
                UpdateRow(TotalRound, calls.Zip(played).Select(t => t.First + t.Second).ToList());
            }
        }

        void UpdateCurrentRound(FinalizedRound final) {
            UpdateRow(Declarations, emptyRow);
            UpdateRow(CurrentRound, emptyRow);
            UpdateRow(TotalRound, FailScore(final));
        }

        void UpdateCurrentRound(int called, int team) {
            List<int> calls = new List<int>() {
                (team == Team)? called : 0,
                (team != Team)? called : 0
            };

            UpdateRow(Declarations, calls);
            if (showPts) {
                UpdateRow(CurrentRound, new List<int>() { 0, 0 });
                UpdateRow(TotalRound, calls);
            }
        }

        void ClearCurrentRound() {
            UpdateRow(Declarations, emptyRow);
            UpdateRow(CurrentRound, emptyRow);
            UpdateRow(TotalRound, emptyRow);
        }

        void ScoreEnter(object sender, PointerEventArgs e) => Rounds.IsVisible = true;
        void ScoreLeave(object sender, PointerEventArgs e) => Rounds.IsVisible = false;

        void CardsWonEnter(object sender, PointerEventArgs e) => CardsWon.IsVisible = true;
        void CardsWonLeave(object sender, PointerEventArgs e) => CardsWon.IsVisible = false;

        void FinalCardsClick(object sender, PointerReleasedEventArgs e) {
            PointerUpdateKind MouseButton = e.GetCurrentPoint(this).Properties.PointerUpdateKind;

            if (MouseButton == PointerUpdateKind.LeftButtonReleased)
                FinalCardsEnter(sender, e);
        }

        void FinalCardsEnter(object sender, PointerEventArgs e) => FinalCardsUpdate(sender, true);
        void FinalCardsLeave(object sender, PointerEventArgs e) => FinalCardsUpdate(sender, false);

        void FinalCardsUpdate(object sender, bool visible) {
            int index;

            if (sender is TextOverlay source) index = FinalCard.IndexOf(source);
            else if (sender is UserInGame user) index = UserText.IndexOf(user);
            else return;

            FinalCard[index].IsVisible = visible;
        }

        bool ShouldReconnect;

        void InitNames(List<string> names) {
            UserText.Swap(1, 2);
            names.Swap(1, 2);
            FinalCard.Swap(1, 2);
            
            SpecCardHolders.Swap(0, 2);
            SpecCardHolders.Swap(2, 3);

            foreach (var (text, name) in UserText.Zip(names.RotateWith(i => i == App.User.Name)))
                text.Text = name;

            int position = UserText.Select(i => i.Text).ToList().IndexOf(names[0]);
            
            UserText = UserText.Rotate(position).ToList();
            TableSegments = TableSegments.Rotate(position).ToList();
            CardTableSegments = CardTableSegments.Rotate(position).ToList();
            FinalCard = FinalCard.Rotate(position).ToList();

            for (int i = 0; i < 4; i++)
                CardTable(i, new CardImage(32) { Opacity = 0 });
            
            UserInGame label = UserText.FirstOrDefault(i => i.Text == App.User.Name);
            if (label != null)
                You = UserText.IndexOf(label);

            ShouldReconnect = false;

            UpdateTime(null, EventArgs.Empty);
            TimeElapsed.IsVisible = true;
        }
        
        public GameView() {
            InitializeComponent();

            UpdateTitleRow(Preferences.MiVi);
            Preferences.MiViChanged += UpdateTitleRow;
            
            timer.Start();

            Timer = new DispatcherTimer() {
                Interval = new TimeSpan(0, 0, 0, 0, 100)
            };
            Timer.Tick += UpdateTime;
            Timer.Start();

            ShouldReconnect = true;

            Discord.Info.Details = $"U igri - {Discord.Info.State}";
            Discord.Info.Party = null;
            Discord.Info.Timestamps = new DiscordRPC.Timestamps(DateTime.UtcNow);

            Program.TimeSpent.Start();
        }

        public GameView(List<string> names): this() => InitNames(names);

        void Loaded(object sender, VisualTreeAttachmentEventArgs e) {
            Network.GameStarted += GameStarted;
            Network.Reconnect += Reconnect;

            Network.TrumpNext += TrumpNext;
            Network.TalonChosen += TalonChosen;
            Network.TrumpChosen += TrumpChosen;
            Network.FullCards += FullCards;

            Network.YouDeclared += YouDeclared;
            Network.PlayerDeclared += PlayerDeclared;

            Network.WinningDeclaration += WinningDeclaration;
            Network.StartPlayingCards += StartPlayingCards;

            Network.AskBela += AskBela;
            Network.YouPlayed += YouPlayed;
            Network.CardPlayed += CardPlayed;

            Network.TableComplete += TableComplete;
            Network.ContinuePlayingCards += ContinuePlayingCards;

            Network.FinalScores += FinalScores;
            Network.FinalCards += FinalCards;
            Network.TotalScore += TotalScore;

            Network.GameFinished += GameFinished;

            if (ShouldReconnect) Requests.Reconnecting();
        }

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Preferences.MiViChanged -= UpdateTitleRow;

            Network.GameStarted -= GameStarted;
            Network.Reconnect -= Reconnect;
            
            Network.TrumpNext -= TrumpNext;
            Network.TalonChosen -= TalonChosen;
            Network.TrumpChosen -= TrumpChosen;
            Network.FullCards -= FullCards;

            Network.YouDeclared -= YouDeclared;
            Network.PlayerDeclared -= PlayerDeclared;

            Network.WinningDeclaration -= WinningDeclaration;
            Network.StartPlayingCards -= StartPlayingCards;

            Network.AskBela -= AskBela;
            Network.YouPlayed -= YouPlayed;
            Network.CardPlayed -= CardPlayed;

            Network.TableComplete -= TableComplete;
            Network.ContinuePlayingCards -= ContinuePlayingCards;

            Network.FinalScores -= FinalScores;
            Network.FinalCards -= FinalCards;
            Network.TotalScore -= TotalScore;

            Network.GameFinished -= GameFinished;

            timer.Stop();
            Timer.Stop();
            Timer.Tick -= UpdateTime;

            Program.TimeSpent.Stop();
        }

        GameState _state;
        GameState State {
            get => _state;
            set {
                _state = value;

                if (_state == GameState.Bidding) Discord.Info.State = "Bira aduta";
                if (_state == GameState.Declaring) Discord.Info.State = "Zvanja";
                if (_state == GameState.Playing) Discord.Info.State = "Karta";

                if (Dealer == You) Discord.Info.State += " na musu";
                Discord.Info.State += $" ({discScores[Team]} - {discScores[1 - Team]})";
            }
        }

        int You = 0;
        int Team => You % 2;

        int _dealer;
        int Dealer {
            get => _dealer;
            set {
                _dealer = value;
                
                for (int i = 0; i < 4; i++)
                    UserText[i].DealerIcon.IsVisible = i == Dealer;
            }
        }

        bool[] DeclareSelected;

        int lastPlaying, lastInTable, selectedTrump;
        CardImage lastPlayed;

        void SetPlaying(int playing) {
            lastPlaying = playing;

            for (int i = 0; i < 4; i++)
                UserText[i].Playing = i == playing;

            if (!App.MainWindow.Spectating && playing == You) {
                Audio.YourTurn();
                
                if (State == GameState.Bidding)
                    Prompt = new BidPrompt(playing == Dealer);

                else if (State == GameState.Declaring)
                    Prompt = new DeclarePrompt(DeclareSelected);

            } else Prompt = null;
        }

        void NextPlaying() => SetPlaying(Utilities.Modulo(lastPlaying + 1, 4));

        bool playWaiting;

        void CardClicked(CardImage sender) {
            if (App.MainWindow.Spectating) return;

            int index = Cards.Children.IndexOf(sender);

            if (State == GameState.Bidding && lastPlaying == You && Dealer == You && index >= 6) {
                Requests.TalonBid(index - 6);

            } if (State == GameState.Declaring && DeclareSelected != null) {
                DeclareSelected[index] = !DeclareSelected[index];

                int margin = 15 * Convert.ToInt32(DeclareSelected[index]);
                sender.Margin = new Thickness(0, -margin, 0, margin);
            
            } else if (State == GameState.Playing && Prompt == null) {
                if (lastPlaying == You) {
                    if (playWaiting) return;
                    playWaiting = true;

                    lastPlayed = sender;
                    Requests.PlayCard(index);

                } else Table(You, new TextOverlay("Niste na potezu", 3000));
            }
        }

        public void GameStarted(int dealer, List<int> cards) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => GameStarted(dealer, cards));
                return;
            }

            ClearCurrentRound();
            ClearTable();
            ClearCardsWon();

            Dealer = dealer;

            State = GameState.Bidding;

            Discord.Logo.SmallImageKey = null;
            Discord.Logo.SmallImageText = null;

            CreateCards(cards);
            Trump = null;

            Score.Opacity = 1;

            SetPlaying(Utilities.Modulo(Dealer + 1, 4));
        }

        void TrumpNext() {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => TrumpNext());
                return;
            }

            if (State != GameState.Bidding) return;

            Table(lastPlaying, new TextOverlay("Dalje"));

            if (App.MainWindow.Spectating)
                SpecCardHolders[lastPlaying].RevealTalon();

            NextPlaying();
        }

        void TalonChosen(int card) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => TalonChosen(card));
                return;
            }

            if (State != GameState.Declaring) return;

            Table(Dealer, new CardImage(card) { MaxHeight = 80 });
        }

        void TrumpChosen(Suit trump) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => TrumpChosen(trump));
                return;
            }

            if (State != GameState.Bidding) return;

            ClearTable();
            
            if (App.MainWindow.Spectating)
                SpecCardHolders.ForEach(i => i.RevealTalon());

            Trump = new Trump(trump, UserText[lastPlaying].Text);
            selectedTrump = lastPlaying;

            Discord.Logo.SmallImageKey = trump.ToString().ToLower();
            Discord.Logo.SmallImageText = lastPlaying % 2 == Team? "Zvao": "Ruši";

            DeclareSelected = new bool[8];
            State = GameState.Declaring;

            SetPlaying(Utilities.Modulo(Dealer + 1, 4));
        }

        void FullCards(List<int> cards) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => FullCards(cards));
                return;
            }

            if (State != GameState.Bidding) return;

            if (App.MainWindow.Spectating) return;

            CreateCards(cards);
        }

        void YouDeclared(bool result) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => YouDeclared(result));
                return;
            }

            if (State != GameState.Declaring) return;
            
            if (result) {
                Table(You, null);
                DeclareSelected = null;
                Prompt = null;

            } else {
                Table(You, new TextOverlay("Nevažeće zvanje", 3000));
                
                (Prompt as DeclarePrompt).Reenable();
            }
        }

        void PlayerDeclared(int value) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => PlayerDeclared(value));
                return;
            }

            if (State != GameState.Declaring) return;

            string declare = (value != 0)? value.ToString() : "Dalje";
            if (value >= Consts.BelotValue) declare = "Belot";
            
            Table(lastPlaying, new TextOverlay(declare));

            if (lastPlaying == Dealer) SetPlaying(-1);
            else NextPlaying();
        }

        void WinningDeclaration(int player, int value, List<int> calls, List<int> teammateCalls) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => WinningDeclaration(player, value, calls, teammateCalls));
                return;
            }

            if (State != GameState.Declaring) return;

            ClearTable();

            foreach (CardImage card in Cards.Children.OfType<CardImage>())
                card.Margin = new Thickness(0);

            Score.Opacity = 0;

            if (value != 0) {
                Table(player, new CardStack(calls));
                Table(Utilities.Modulo(player + 2, 4), new CardStack(teammateCalls));

                Alert = new TextOverlay(value >= Consts.BelotValue? "Belot" : value.ToString());

            } else Alert = new TextOverlay("Nema zvanja");

            UpdateCurrentRound(value, Position(player) % 2);
        }

        void StartPlayingCards() {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(StartPlayingCards);
                return;
            }

            if (State != GameState.Declaring) return;

            ClearTable();
            Alert = null;
            State = GameState.Playing;

            Score.Opacity = 1;

            SetPlaying(Utilities.Modulo(Dealer + 1, 4));
            lastInTable = Utilities.Modulo(lastPlaying - 1, 4);
        }

        void AskBela() {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(AskBela);
                return;
            }

            if (State != GameState.Playing) return;

            lastPlayed.Margin = new Thickness(0, -15, 0, 15);

            Prompt = new BelaPrompt();
        }

        void YouPlayed(int card) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => YouPlayed(card));
                return;
            }

            if (State != GameState.Playing) return;

            playWaiting = false;

            if (card != -1) {
                Cards.Children.RemoveAt(card);
                
                if (!Cards.Children.Any()) Cards.Parent.Opacity = 0;

                Prompt = null;

            } else Table(You, new TextOverlay("Neispravna karta", 3000));
        }

        void CardPlayed(int card, bool bela) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => CardPlayed(card, bela));
                return;
            }

            if (State != GameState.Playing) return;
            
            Table(lastPlaying, null);
            CardTable(lastPlaying, new CardImage(card));

            if (bela)
                Table(lastPlaying, new TextOverlay("Bela"));

            cardsOnTable.Add(card);

            if (App.MainWindow.Spectating)
                SpecCardHolders[lastPlaying].CardPlayed(card);
            
            if (lastPlaying == lastInTable) SetPlaying(-1);
            else NextPlaying();
        }

        void TableComplete(List<int> calls, FinalizedRound played) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => TableComplete(calls, played));
                return;
            }

            if (State != GameState.Playing) return;

            UpdateCurrentRound(calls, played.Score);

            if (played.Fail) {
                Table(selectedTrump, new TextOverlay(new Image() {
                    Height = 70,
                    Source = App.GetImage("Edgar")
                }));

                Audio.Fail();
            }
        }

        void ContinuePlayingCards(int winner) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => ContinuePlayingCards(winner));
                return;
            }

            if (State != GameState.Playing) return;
            
            ClearTable();

            UpdateCardsWon(winner);

            SetPlaying(winner);
            lastInTable = Utilities.Modulo(lastPlaying - 1, 4);
        }

        void FinalScores(FinalizedRound final, int lastRoundWinner) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => FinalScores(final, lastRoundWinner));
                return;
            }

            if (State != GameState.Playing) return;
            
            ClearTable();

            UpdateCardsWon(lastRoundWinner);

            SetPlaying(-1);

            UpdateCurrentRound(final);
        }

        void FinalCards(int player, List<int> cards, List<int> talon) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => FinalCards(player, cards, talon));
                return;
            }

            if (State != GameState.Playing) return;

            FinalCard[player].SetControl(new CardStack(cards, talon));
        }

        void TotalScore(FinalizedRound final, List<int> total) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => TotalScore(final, total));
                return;
            }

            if (State != GameState.Playing) return;
            
            RoundRow row = new RoundRow() { Icon = $"Suits/{final.Suit}" };
            
            bool temp = Rounds.IsVisible;
            Rounds.IsVisible = false;
            Rounds.IsVisible = temp;

            UpdateRow(row, FailScore(final));
            Rounds.Children.Add(row);

            UpdateRow(Total, discScores = total);
            Total.IsVisible = true;
        }

        void GameFinished(List<int> score, Room room, string password) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => GameFinished(score, room, password));
                return;
            }

            room.Password = password;
            
            App.MainWindow.View = new InRoomView(score, room);
        }

        void Reconnect(Room room, List<FinalizedRound> history, TimeSpan timeElapsed) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => Reconnect(room, history, timeElapsed));
                return;
            }

            App.MainWindow.Title = room.Name;
            Discord.Info.Details = $"U igri - {room.Name}";

            startOffset = timeElapsed;

            InitNames(room.Users.Select(i => i.Name).ToList());

            List<int> total = Enumerable.Range(0, 2).Select(i => history.Sum(j => j.Score[i])).ToList();

            showPts = room.ShowPts;

            State = GameState.Playing;

            foreach (FinalizedRound round in history)
                TotalScore(round, total);

            State = GameState.Bidding;
        }

        static bool SpecCardsState = false;

        public void Spectate() {
            YourCardsHolder.IsVisible = false;
            SpecCardHolders.ForEach(i => i.IsVisible = SpecCardsState);
            GameGrid.Margin = new Thickness(5);
            CardsWonRoot.IsVisible = false;
            HideSpecCardsButton.IsVisible = true;
        }

        void HideSpecCards() {
            SpecCardsState = !SpecCardsState;
            SpecCardHolders.ForEach(i => i.IsVisible = SpecCardsState);
        }
    }
}
