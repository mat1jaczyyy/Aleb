﻿using System;
using System.Collections.Generic;
using System.Linq;

using Aleb.Common;

namespace Aleb.Server {
    class Room {
        public static List<Room> Rooms { get; private set; } = new List<Room>();

        public static Room Create(string name, GameType? type, int goal, bool showpts, string password, User creator) {
            if (!Validation.ValidateRoomName(name)) return null;
            if (type == null) return null;
            if (!Validation.ValidateRoomGoal(goal)) return null;
            if (!Validation.ValidateRoomPassword(password)) return null;

            if (Rooms.Any(i => i.Name == name || i.Everyone.Contains(creator))) return null;

            Room room = new Room(name, type.Value, goal, showpts, password, creator);
            Rooms.Add(room);

            return room;
        }

        public void Destroy() {
            Spectators.ForEach(i => i?.Client?.Send(new Message("SpectatingOver")));

            Rooms.Remove(this);
        }

        public class Person {
            public readonly User User;
            public bool Ready = false;

            public Person(User user) => User = user;
        }

        public List<Person> People { get; private set; } = new List<Person>();
        public List<User> Users => People.Select(i => i.User).ToList();
        public List<User> Spectators { get; private set; } = new List<User>();
        public IEnumerable<User> Everyone => Users.Concat(Spectators);

        public int Count => People.Count;
        
        public readonly string Name;
        public readonly GameType Type;
        public readonly int ScoreGoal;
        public readonly bool ShowPts;
        public readonly string Password;

        public bool HasPassword => Password != "";

        public bool Spectate(User spectating, string password) {
            if (Everyone.Contains(spectating)) return false;
            if (HasPassword && Password != password) return false;

            Spectators.Add(spectating);
            spectating.State = UserState.Spectating;
            return true;
        }

        public bool SpectatorLeave(User leaving) {
            if (!Spectators.Contains(leaving)) return false;

            Spectators.Remove(leaving);
            leaving.State = UserState.Idle;

            return true;
        }

        public bool Join(User joining, string password) {
            if (Count >= 4) return false;
            if (Everyone.Contains(joining)) return false;
            if (HasPassword && Password != password) return false;

            People.Add(new Person(joining));
            joining.State = UserState.InRoom;
            return true;
        }

        public bool Leave(User leaving) {
            if (!Users.Contains(leaving)) return false;

            if (Game != null)
                DestroyGame();

            People.Remove(People.First(i => i.User == leaving));
            leaving.State = UserState.Idle;

            if (Count == 0) Destroy();

            return true;
        }

        public bool Switch(User[] switching) {
            if (Game != null) return false;
            if (switching == null || switching.Length != 2) return false;
            if (!switching.All(Users.Contains)) return false;
            if (switching[0] == switching[1]) return false;

            List<int> indexes = switching.Select(i => Users.IndexOf(i)).ToList();
            indexes.ForEach(i => People[i].Ready = false);

            People.Swap(indexes[0], indexes[1]);
            
            return true;
        }

        public bool SetReady(User user, bool state) {
            Person person = People.FirstOrDefault(i => i.User == user);

            if (person != null) {
                person.Ready = state;
                return true;
            }
            
            return false;
        }

        public Game Game { get; private set; }

        public bool GameCompleted(int delay = 0) {
            if (Game?.Score[0] == Game?.Score[1])
                return false;

            for (int i = 0; i < 2; i++)
                if (Game?.Score[i] >= ScoreGoal) {
                    DestroyGame(delay);
                    return true;
                }

            return false;
        }

        public void BelotCompleted(int team) => DestroyGame(15000, team);

        void DestroyGame(int delay = 0, int belot = -1) {
            IEnumerable<int> score = Game.Score.Select((x, i) => i == belot ? Consts.BelotValue : x);

            RecordedMatch recording = Game.Recording;
            recording.RoomName = Name;
            recording.Score = score.ToArray();
            recording.Users = Users.Select(i => i.Name).ToArray();
            recording.Date = DateTime.UtcNow;

            int winTeam = Array.IndexOf(Game.Score, Game.Score.Max());
            Users[winTeam * 2].GamesWon++;
            Users[winTeam * 2 + 1].GamesWon++;
            Users[(1 - winTeam) * 2].GamesLost++;
            Users[(1 - winTeam) * 2 + 1].GamesLost++;

            Users[winTeam * 2].MaxPointsMatch = Math.Max(Users[winTeam * 2].MaxPointsMatch, Game.Score[winTeam]);
            Users[winTeam * 2 + 1].MaxPointsMatch = Math.Max(Users[winTeam * 2 + 1].MaxPointsMatch, Game.Score[winTeam]);
            Users[(1 - winTeam) * 2].MaxPointsMatch = Math.Max(Users[winTeam * 2].MaxPointsMatch, Game.Score[1 - winTeam]);
            Users[(1 - winTeam) * 2 + 1].MaxPointsMatch = Math.Max(Users[winTeam * 2 + 1].MaxPointsMatch, Game.Score[1 - winTeam]);

            Message msg = new Message("GameFinished", score.ToStr(), ToString(), Password);

            foreach (User user in Everyone) {
                if (user.State != UserState.Spectating) {
                    user.CompletedGame(recording);
                    user.State = UserState.InRoom;
                }

                user.Client.Send(delay, msg);
            }
            
            Game = null;
        }

        public bool Start(User starting) {
            if (Game != null || starting != People[0].User || Count < 4 || People.Any(i => !i.Ready))
                return false;

            Game = new Game(this);
            People.ForEach(i => {
                i.Ready = false;
                i.User.State = UserState.InGame;
                i.User.GamesPlayed++;
            });

            return true;
        }

        Room(string name, GameType type, int goal, bool showpts, string password, User owner) {
            Name = name;
            Type = type;
            ScoreGoal = goal;
            ShowPts = showpts;
            Password = password;

            Join(owner, password);
        }

        public override string ToString() => $"{Name},{Type},{ScoreGoal},{ShowPts},{HasPassword},{Count},{People.ToStr(i => i.User.Name)}";
    }
}
