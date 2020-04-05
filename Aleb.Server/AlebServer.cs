﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Aleb.Common;

namespace Aleb.Server {
    static class AlebServer {
        static TcpListener server;

        static async void Handshake(TcpClient tcp) {
            AlebClient client = new AlebClient(tcp);

            Console.WriteLine($"Connection from {client.Address}");

            client.Send("Version", Protocol.Version);
            
            User user = null;

            try {
                bool success = false;

                do {
                    (string name, string password) = await client.ReadMessage(i => (i.Args[0], i.Args[1]), ("", ""), "Login");
                    client.Send("LoginResult", success = (user = User.Connect(name, password, client)) != null);

                } while (!success);

            } catch {
                Console.WriteLine($"{client.Address} disconnected without logging in");
                client.Dispose();
                return;
            }

            Console.WriteLine($"{user.Name} logged in from {client.Address}");
        }

        static void Main(string[] args) {
            AlebClient.LogCommunication = true;

            string bind = Protocol.Localhost;

            if (args.Length == 2 && args[0] == "--bind") bind = args[1];
            else if (args.Length != 0) {
                Console.Error.WriteLine("Invalid arguments.");
                return;
            }

            server = new TcpListener(
                Dns.GetHostEntry(bind).AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork),
                Protocol.Port
            );

            try {
                server.Start();

            } catch {
                Console.Error.WriteLine("Failed to start server! Is the port already in use?");
                return;
            }

            Console.WriteLine($"Aleb Server started at {server.LocalEndpoint}");

            while (true) Handshake(server.AcceptTcpClient());
        }
    }
}
