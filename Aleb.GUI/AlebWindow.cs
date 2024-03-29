﻿using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

using Aleb.Client;
using Aleb.GUI.Components;
using Aleb.GUI.Views;

namespace Aleb.GUI {
    class AlebWindow: Window {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            Root = this.Get<Grid>("Root");
            Canvas = this.Get<Canvas>("Canvas");
            ContentRoot = this.Get<Grid>("ContentRoot");

            TitleText = this.Get<TextBlock>("Title");
            PopupTitle = this.Get<TextBlock>("PopupTitle");
            
            PreferencesButton = this.Get<PreferencesButton>("PreferencesButton");
            PinButton = this.Get<PinButton>("PinButton");
            PopupClose = this.Get<Close>("PopupClose");

            view = this.Get<Border>("View");
            popup = this.Get<Border>("Popup");
            profile = this.Get<Border>("Profile");
            
            PopupContainer = this.Get<Grid>("PopupContainer");
            ProfileContainer = this.Get<Border>("ProfileContainer");

            SpectatorsRoot = this.Get<StackPanel>("SpectatorsRoot");
            Spectators = this.Get<TextBlock>("Spectators");
            SpectatorLeaveIcon = this.Get<LeaveIcon>("SpectatorLeaveIcon");

            YourProfile = this.Get<ProfileButton>("YourProfile");
        }
        
        Grid Root, ContentRoot;
        Canvas Canvas;

        TextBlock TitleText;
        public TextBlock PopupTitle;

        PreferencesButton PreferencesButton;
        PinButton PinButton;
        public Close PopupClose;

        Border view, popup, profile;
        Grid PopupContainer;
        Border ProfileContainer;

        StackPanel SpectatorsRoot;
        TextBlock Spectators;
        LeaveIcon SpectatorLeaveIcon;

        public ProfileButton YourProfile { get; private set; }

        double VirtualWidth {
            set {
                Canvas.Width = value;
                ContentRoot.Width = value;
            }
        }

        double VirtualHeight {
            set {
                Canvas.Height = value;
                ContentRoot.Height = value;
            }
        }

        public Control View {
            get => (Control)view.Child;
            set {
                view.Child = value;

                SpectatorsRoot.IsVisible = value is ISpectateable;

                if (View is ISpectateable spec && Spectating)
                    spec.Spectate();
            }
        }

        public bool Spectating {
            get => SpectatorLeaveIcon.IsVisible;
            set {
                SpectatorLeaveIcon.IsVisible = value;
                
                if (View is ISpectateable spec)
                    spec.Spectate();
            }
        }

        public Control Popup {
            get => (Control)popup.Child;
            set {
                popup.Child = value;

                PreferencesButton.Enabled = Popup == null;
                PinButton.Enabled = Popup == null;

                PopupClose.IsEnabled = true;
                PopupContainer.IsVisible = Popup != null;
            }
        }

        public Control Profile {
            get => (Control)profile.Child;
            set {
                profile.Child = value;
                ProfileContainer.IsVisible = Profile != null;
                SizeUpdated();
            }
        }

        public new string Title {
            set => TitleText.Text = base.Title = value == ""? "Aleb" : $"Aleb - {value}";
        }

        public AlebWindow() {
            InitializeComponent();
            #if DEBUG
                this.AttachDevTools();
            #endif
            
            BringToTop();
        }

        IDisposable observable;

        void Loaded(object sender, EventArgs e) {
            Position = new PixelPoint(Position.X, Math.Max(0, Position.Y));

            View = new ConnectingView(true);

            observable = this.GetObservable(ClientSizeProperty).Subscribe(SizeUpdated);

            Network.SpectatorCount += SpectatorCount;
            Network.SpectatingOver += SpectatingOver;
        }

        void Unloaded(object sender, CancelEventArgs e) {
            observable?.Dispose();
            
            Network.SpectatorCount -= SpectatorCount;
            Network.SpectatingOver -= SpectatingOver;
        }
        
        void SizeUpdated() => SizeUpdated(ClientSize);

        void SizeUpdated(Size size) {
            double width = 960.0 + (Profile != null? ProfileContainer.Width : 0);
            double height = 540.0;
            
            double target = width / height;
            double aspect = size.Width / size.Height;
            
            VirtualWidth = (aspect <= target)? width : (height * aspect);
            VirtualHeight = (aspect >= target)? height : (width / aspect);

            double scale = Math.Min(size.Width / width, size.Height / height);
            Root.RenderTransform = new ScaleTransform(scale, scale);
        }

        void Window_KeyDown(object sender, KeyEventArgs e) {

        }

        public void BringToTop() {
            Topmost = true;
            Topmost = Preferences.Topmost;
            Activate();
        }

        void MoveWindow(object sender, PointerPressedEventArgs e) {
            if (e.ClickCount == 2) Maximize(null);
            else BeginMoveDrag(e);

            BringToTop();
        }
        
        void Minimize() => WindowState = WindowState.Minimized;

        void Maximize(PointerEventArgs e) {
            WindowState = (WindowState == WindowState.Maximized)? WindowState.Normal : WindowState.Maximized;
            
            if (e?.KeyModifiers == App.ControlKey && WindowState == WindowState.Maximized) {
                Screen result = null;

                foreach (Screen screen in Screens.All)
                    if (screen.Bounds.Contains(Position)) {
                        result = screen;
                        break;
                    }

                if (result != null) {
                    Width = result.Bounds.Width;
                    Height = result.Bounds.Height;
                }
            }

            BringToTop();
        }

        void ResizeNorthWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthWest, e);
        void ResizeNorth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.North, e);
        void ResizeNorthEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.NorthEast, e);
        void ResizeWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.West, e);
        void ResizeEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.East, e);
        void ResizeSouthWest(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthWest, e);
        void ResizeSouth(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.South, e);
        void ResizeSouthEast(object sender, PointerPressedEventArgs e) => BeginResizeDrag(WindowEdge.SouthEast, e);

        void ClosePopup() => Popup = null;
        void CloseProfile() => Profile = null;

        void SpectatorCount(int count) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(() => SpectatorCount(count));
                return;
            }

            Spectators.Text = count.ToString();
            SpectatorsRoot.Opacity = count <= 0? 0 : 1;
        }
        
        void SpectatingOver() {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.InvokeAsync(SpectatingOver);
                return;
            }

            if (!Spectating) return;

            Spectating = false;
            
            View = new RoomListView();
        }

        void SpectatorLeave() {
            if (!Spectating) return;
            
            Requests.SpectatorLeave();
            SpectatingOver();
        }
    }
}