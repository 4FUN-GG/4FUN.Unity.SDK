using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FourFun
{
    /// <summary>
    /// Provides an interface for communicating with the game server.
    /// </summary>
    public class GameServerInterface
    {
        // Define constants for server communication
        private const ushort serverPort = 21037;
        private static readonly IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Loopback, serverPort);

        // Current state of the game
        private State gameState = State.Loading;

        /// <summary>
        /// Sends a command to the game server and returns the reply.
        /// </summary>
        /// <param name="command">The command to send to the server.</param>
        /// <returns>The reply received from the server.</returns>
        private string SendCommand(string command)
        {
            using (Socket socket = new Socket(serverEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Connect(serverEndpoint);
                byte[] commandBytes = Encoding.Unicode.GetBytes(command);
                socket.Send(commandBytes, SocketFlags.None);
                socket.Shutdown(SocketShutdown.Send);

                return ReceiveReply(socket);
            }
        }

        /// <summary>
        /// Receives a reply from the server.
        /// </summary>
        /// <param name="socket">The socket used to receive the reply.</param>
        /// <returns>The received message from the server.</returns>
        private string ReceiveReply(Socket socket)
        {
            var buffer = new byte[200];
            int totalBytesReceived = 0;

            while (true)
            {
                int bytesReceived = socket.Receive(buffer, totalBytesReceived, buffer.Length - totalBytesReceived, SocketFlags.None);
                if (bytesReceived <= 0)
                    break;

                totalBytesReceived += bytesReceived;

                // Expand buffer if needed
                if (buffer.Length - totalBytesReceived < 100)
                {
                    Array.Resize(ref buffer, buffer.Length * 2);
                }
            }

            socket.Shutdown(SocketShutdown.Both);
            return Encoding.Unicode.GetString(buffer, 0, totalBytesReceived);
        }

        /// <summary>
        /// Sets the game state to finished and informs the server.
        /// </summary>
        /// <exception cref="UnityException">Thrown if the command fails.</exception>
        public void SetFinished()
        {
            if (Application.isEditor)
                return;
            gameState = State.Finished;
            if ("MSG_OKAY" != SendCommand("SET_FINISHED"))
            {
                throw new UnityException("Couldn't call 'SET_FINISHED' on game server.");
            }
        }

        /// <summary>
        /// Sets the game state to Playing and sends a command to the server to indicate that the game has loaded.
        /// </summary>
        /// <exception cref="UnityException">Thrown if the command to set loaded fails.</exception>
        public void SetLoaded()
        {
            gameState = State.Playing;

            if (Application.isEditor)
                return;

            string response = SendCommand("SET_LOADED");
            if (response != "MSG_OKAY")
            {
                throw new UnityException("Failed to set the loaded state on the server.");
            }
        }


        /// <summary>
        /// Checks if the game launcher is visible.
        /// </summary>
        /// <returns>True if the launcher is visible; otherwise, false.</returns>
        /// <exception cref="UnityException">Thrown if the response from the server is invalid.</exception>
        public bool IsLauncherVisible()
        {
            if (Application.isEditor)
                return false;
            string response = SendCommand("GET_LAUNCHERSTATE");
            return response switch
            {
                "MSG_OKAY" => false,
                "MSG_FAILED" => true,
                _ => throw new UnityException("Invalid response from Game Server.")
            };
        }

        /// <summary>
        /// Retrieves the player places from the server.
        /// </summary>
        /// <returns>A list of booleans indicating player placements.</returns>
        /// <exception cref="UnityException">Thrown if the response is invalid.</exception>
        public List<bool> GetPlayerPlaces()
        {
            string response = SendCommand("GET_PLAYERPLACES");

            if (response.Length <= 9 || !response.StartsWith("MSG_DATA#"))
                throw new UnityException("Invalid Player Places Response");

            var playerPlaces = new List<bool>();

            foreach (char c in response.Substring(9))
            {
                playerPlaces.Add(c switch
                {
                    '0' => false,
                    '1' => true,
                    _ => throw new UnityException("Invalid player place.")
                });
            }

            return playerPlaces;
        }

        /// <summary>
        /// Sends a "keep-alive" message to the server.
        /// </summary>
        public void SendAlive()
        {
            if (Application.isEditor)
                return;
            ThreadPool.QueueUserWorkItem(_ => SendCommand("MSG_ALIVE"));
        }

        /// <summary>
        /// Sets the high score for a specific player.
        /// </summary>
        /// <param name="playerIndex">The index of the player whose score is being set.</param>
        /// <param name="score">The score to set.</param>
        /// <exception cref="UnityException">Thrown if there is an error setting the high score.</exception>
        public void SetHighscore(int playerIndex, int score)
        {
            if (Application.isEditor)
                return;
            string command = $"SET_HIGHSCORE#{playerIndex:X02}{score:X08}";
            string response = SendCommand(command);

            if (response != "MSG_OKAY")
            {
                throw new UnityException("Error setting highscore.");
            }
        }

        /// <summary>
        /// Represents the state of the game.
        /// </summary>
        private enum State
        {
            Loading,
            Playing,
            Finished,
            Error
        }
    }
}
