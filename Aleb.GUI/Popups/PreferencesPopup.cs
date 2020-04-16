﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Humanizer;
using Humanizer.Localisation;

using Aleb.Client;
using Aleb.Common;
using Aleb.GUI.Components;
using Aleb.GUI.Views;

namespace Aleb.GUI.Popups {
    public class PreferencesPopup: UserControl {
        void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            App.MainWindow.PopupTitle.Text = "Postavke";

            MiVi = this.Get<RadioButton>("MiVi");
            ViOni = this.Get<RadioButton>("ViOni");

            DiscordPresence = this.Get<CheckBox>("DiscordPresence");

            CurrentSession = this.Get<TextBlock>("CurrentSession");
            AllTime = this.Get<TextBlock>("AllTime");

            Version = this.Get<TextBlock>("Version");
        }

        RadioButton MiVi, ViOni;
        CheckBox DiscordPresence;

        TextBlock CurrentSession, AllTime, Version;
        DispatcherTimer Timer;

        void UpdateTime(object sender, EventArgs e) {
            CurrentSession.Text = $"Trenutna sesija: {Program.TimeSpent.Elapsed.Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Hour)}";

            if (Preferences.Time >= (long)TimeSpan.MaxValue.TotalSeconds) Preferences.BaseTime = 0;

            AllTime.Text = $"Sveukupno: {Preferences.Time.Seconds().Humanize(minUnit: TimeUnit.Second, maxUnit: TimeUnit.Hour)}";
        }

        public PreferencesPopup() {
            InitializeComponent();

            Version.Text += Program.Version;

            if (App.AvaloniaVersion() != "")
                ToolTip.SetTip(Version, $"Avalonia {App.AvaloniaVersion()}");

            UpdateTime(null, EventArgs.Empty);
            Timer = new DispatcherTimer() {
                Interval = new TimeSpan(0, 0, 1)
            };
            Timer.Tick += UpdateTime;
            Timer.Start();

            DiscordPresence.IsChecked = Preferences.DiscordPresence;

            MiVi.IsChecked = Preferences.MiVi;
            ViOni.IsChecked = !Preferences.MiVi;
        }

        void DiscordPresence_Changed(object sender, RoutedEventArgs e) => Preferences.DiscordPresence = DiscordPresence.IsChecked.Value;
        
        void MiVi_Changed(object sender, RoutedEventArgs e) => Preferences.MiVi = true;
        void ViOni_Changed(object sender, RoutedEventArgs e) => Preferences.MiVi = false;

        void OpenCrashesFolder(object sender, RoutedEventArgs e) {
            if (!Directory.Exists(Program.UserPath)) Directory.CreateDirectory(Program.UserPath);
            if (!Directory.Exists(Program.CrashDir)) Directory.CreateDirectory(Program.CrashDir);

            App.URL(Program.CrashDir);
        }

        void Loaded(object sender, VisualTreeAttachmentEventArgs e) {}

        void Unloaded(object sender, VisualTreeAttachmentEventArgs e) {
            Timer.Stop();
            Timer.Tick -= UpdateTime;
        }
    }
}
