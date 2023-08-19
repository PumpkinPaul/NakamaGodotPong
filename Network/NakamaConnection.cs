// Copyright Pumpkin Games Ltd. All Rights Reserved.

//Based on code from the FishGame Unity sample from Herioc Labs.
//https://github.com/heroiclabs/fishgame-unity/blob/main/FishGame/Assets/Nakama/NakamaConnection.cs

using Nakama;
using NakamaGodotPong.Players;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NakamaGodotPong.Network;

/// <summary>
/// Facade into the Nakama system - responsible for connection and matchmaking.
/// </summary>
public class NakamaConnection
{
    readonly GodotLogger _logger;

    public string Scheme = "http";
    public string Host = "localhost";
    public int Port = 7350;
    public string ServerKey = "defaultkey";

    public IClient Client;
    public ISession Session;
    public ISocket Socket;

    readonly PlayerProfile _playerProfile;

    string _currentMatchmakingTicket;

    public NakamaConnection(
        GodotLogger logger,
        PlayerProfile playerProfile)
    {
        _logger = logger;
        _playerProfile = playerProfile;
    }

    /// <summary>
    /// Connects to the Nakama server using device authentication and opens socket for realtime communication.
    /// </summary>
    public async Task Connect()
    {
        _logger.DebugFormat("==================================================");
        _logger.DebugFormat($"Nakama Connection");
        _logger.DebugFormat("==================================================");
        _logger.DebugFormat($"Create Client...");

        // Connect to the Nakama server.
        Client = new Client(Scheme, Host, Port, ServerKey);

        // Attempt to restore an existing user session.
        var authToken = _playerProfile.SessionToken;
        if (!string.IsNullOrEmpty(authToken))
        {
            _logger.DebugFormat($"Restore Session");
            var session = Nakama.Session.Restore(authToken);
            if (!session.IsExpired)
            {
                Session = session;
            }
        }

        // If we weren't able to restore an existing session, authenticate to create a new user session.
        if (Session == null)
        {
            // If we've already stored a device identifier in PlayerPrefs then use that.
            if (string.IsNullOrWhiteSpace(_playerProfile.DeviceIdentifier))
            {
                // Store the device identifier to ensure we use the same one each time from now on.
                _playerProfile.DeviceIdentifier = Guid.NewGuid().ToString();
            }

            _logger.DebugFormat($"Authenticate Device...");
            // Use Nakama Device authentication to create a new session using the device identifier.
            Session = await Client.AuthenticateDeviceAsync(_playerProfile.DeviceIdentifier);

            // Store the auth token that comes back so that we can restore the session later if necessary.
            _playerProfile.SessionToken = Session.AuthToken;

            _playerProfile.Save();
        }

        _logger.DebugFormat("==================================================");
        _logger.DebugFormat("Nakama Session Details");
        _logger.DebugFormat("==================================================");
        _logger.DebugFormat($"AuthToken: {Session.AuthToken}");       // raw JWT token
        _logger.DebugFormat($"RefreshToken: {Session.RefreshToken}"); // raw JWT token.
        _logger.DebugFormat($"UserId: {Session.UserId}");
        _logger.DebugFormat($"Username: {Session.Username}");
        _logger.DebugFormat($"Session has expired: {Session.IsExpired}");
        _logger.DebugFormat($"Session expires at: {Session.ExpireTime}");

        // Open a new Socket for realtime communication.
        _logger.DebugFormat($"Connect a Socket...");
        Socket = Nakama.Socket.From(Client);
        await Socket.ConnectAsync(Session, true);
    }

    /// <summary>
    /// Starts looking for a match with a given number of minimum players.
    /// </summary>
    public async Task FindMatch(int minPlayers = 2)
    {
        _logger.DebugFormat("==================================================");
        _logger.DebugFormat($"Find match...");
        _logger.DebugFormat("==================================================");

        // Set some matchmaking properties to ensure we only look for games that are using the Unity client.
        // This is not a required when using the Unity Nakama SDK, 
        // however in this instance we are using it to differentiate different matchmaking requests across multiple platforms using the same Nakama server.
        var matchmakingProperties = new Dictionary<string, string>
        {
            { "engine", "fna" }
        };

        // Add this client to the matchmaking pool and get a ticket.
        var matchmakerTicket = await Socket.AddMatchmakerAsync("+properties.engine:fna", minPlayers, minPlayers, matchmakingProperties);
        _currentMatchmakingTicket = matchmakerTicket.Ticket;

        _logger.DebugFormat($"matchmakerTicket: {_currentMatchmakingTicket}");
    }

    /// <summary>
    /// Cancels the current matchmaking request.
    /// </summary>
    public async Task CancelMatchmaking()
    {
        _logger.DebugFormat("==================================================");
        _logger.DebugFormat($"Cancel matchmaking: {_currentMatchmakingTicket}");
        _logger.DebugFormat("==================================================");

        await Socket.RemoveMatchmakerAsync(_currentMatchmakingTicket);
    }
}
