// Copyright Pumpkin Games Ltd. All Rights Reserved.

//https://github.com/heroiclabs/fishgame-unity/blob/main/FishGame/Assets/Managers/GameManager.cs

using Godot;
using Nakama;
using NakamaGodotPong.Players;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NakamaGodotPong.Network;

public record SpawnedPlayerEventArgs(
    Player Player,
    string SessionId
);

public record ReceivedRemotePlayerPaddleStateEventArgs(
float TotalSeconds,
    Vector2 Position,
    Vector2 Velocity,
    Vector2 TankInput,
    string SessionId
);

public record ReceivedRemoteBallStateEventArgs(
    float Direction,
    Vector2 Position
);

public record ReceivedRemoteScoreEventArgs(
    int Player1Score,
    int Player2Score
);

public record RemovedPlayerEventArgs(
    string SessionId,
    Player Player
);

/// <summary>
/// Responsible for managing a networked game
/// </summary>
public class NetworkGameManager
{
    public event EventHandler<SpawnedPlayerEventArgs> SpawnedLocalPlayer;
    public event EventHandler<SpawnedPlayerEventArgs> SpawnedRemotePlayer;
    public event EventHandler<ReceivedRemotePlayerPaddleStateEventArgs> ReceivedRemotePlayerPaddleState;
    public event EventHandler<ReceivedRemoteBallStateEventArgs> ReceivedRemoteBallState;
    public event EventHandler<ReceivedRemoteScoreEventArgs> ReceivedRemoteScore;
    public event EventHandler<RemovedPlayerEventArgs> RemovedPlayer;

    readonly GodotLogger _logger;

    //Multiplayer
    readonly NakamaConnection _nakamaConnection;

    IUserPresence _hostPresence;
    IUserPresence _localUserPresence;
    IMatch _currentMatch;

    public IDictionary<string, Player> Players { get; private set; }
    Player _localPlayer;

    public bool IsHost => (_hostPresence?.SessionId ?? "host") == (_localUserPresence?.SessionId ?? "user");

    public NetworkGameManager(
        GodotLogger logger,
        NakamaConnection nakamaConnection)
    {
        _logger = logger;
        _nakamaConnection = nakamaConnection;

        Players = new Dictionary<string, Player>();
    }

    public async Task Connect()
    {
        await _nakamaConnection.Connect();

        _nakamaConnection.Socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;
        _nakamaConnection.Socket.ReceivedMatchPresence += OnReceivedMatchPresence;
        _nakamaConnection.Socket.ReceivedMatchState += OnReceivedMatchState;
    }

    /// <summary>
    /// Called when a MatchmakerMatched event is received from the Nakama server.
    /// </summary>
    /// <param name="matched">The MatchmakerMatched data.</param>
    public async void OnReceivedMatchmakerMatched(IMatchmakerMatched matched)
    {
        _logger.DebugFormat($"OnReceivedMatchmakerMatched");

        //Set the host - hosts will be responsible for sending non-player data (e.g. like the ball's position)
        _hostPresence = matched.Users.OrderByDescending(x => x.Presence.SessionId).First().Presence;

        // Cache a reference to the local user.
        _localUserPresence = matched.Self.Presence;

        // Join the match.
        var match = await _nakamaConnection.Socket.JoinMatchAsync(matched);

        // Spawn a player instance for each connected user.
        foreach (var user in match.Presences)
            SpawnPlayer(match.Id, user);

        // Cache a reference to the current match.
        _currentMatch = match;
    }

    /// <summary>
    /// Called when a player/s joins or leaves the match.
    /// </summary>
    /// <param name="matchPresenceEvent">The MatchPresenceEvent data.</param>
    public void OnReceivedMatchPresence(IMatchPresenceEvent matchPresenceEvent)
    {
        _logger.DebugFormat($"OnReceivedMatchPresence");

        //Set a new host if current host leaves
        if (matchPresenceEvent.Leaves.Any(x => x.UserId == _hostPresence.UserId))
            _hostPresence = _currentMatch.Presences.OrderBy(x => x.SessionId).First();

        // For each new user that joins, spawn a player for them.
        foreach (var user in matchPresenceEvent.Joins)
            SpawnPlayer(matchPresenceEvent.MatchId, user);

        // For each player that leaves, despawn their player.
        foreach (var user in matchPresenceEvent.Leaves)
            RemovePlayer(user.SessionId);
    }

    /// <summary>
    /// Called when new match state is received.
    /// </summary>
    /// <param name="matchState">The MatchState data.</param>
    public void OnReceivedMatchState(IMatchState matchState)
    {
        if (!Players.TryGetValue(matchState.UserPresence.SessionId, out var player))
            return;

        //a If the incoming data is not related to this remote player, ignore it and return early.
        var networkPlayer = player as RemotePlayer;
        if (matchState.UserPresence.SessionId != networkPlayer?.NetworkData?.User?.SessionId)
            return;

        // Decide what to do based on the Operation Code of the incoming state data as defined in OpCodes.
        switch (matchState.OpCode)
        {
            case OpCodes.PADDLE_PACKET:
                //UpdateTankStateFromState(matchState.State, networkPlayer);
                UpdateTankStateFromState(matchState.State, networkPlayer);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Quits the current match.
    /// </summary>
    public async Task QuitMatch()
    {
        _logger.DebugFormat($"QuitMatch");

        // Ask Nakama to leave the match.
        await _nakamaConnection.Socket.LeaveMatchAsync(_currentMatch);

        // Reset the currentMatch and localUser variables.
        _currentMatch = null;
        _localUserPresence = null;

        // Destroy all existing player.
        foreach (var player in Players.Values)
            player.Destroy();

        // Clear the players array.
        Players.Clear();
    }

    void SpawnPlayer(string matchId, IUserPresence userPresence)
    {
        _logger.DebugFormat($"SpawnPlayer: {userPresence}");

        // If the player has already been spawned, return early.
        if (Players.ContainsKey(userPresence.SessionId))
        {
            return;
        }

        // Set a variable to check if the player is the local player or not based on session ID.
        var isLocal = userPresence.SessionId == _localUserPresence.SessionId;

        Player player;

        // Setup the appropriate network data values if this is a remote player.
        // If this is our local player, add a listener for the PlayerDied event.
        if (isLocal)
        {
            player = new LocalPlayer();
            _localPlayer = player;

            SpawnedLocalPlayer?.Invoke(this, new SpawnedPlayerEventArgs(player, userPresence.SessionId));
        }
        else
        {
            player = new RemotePlayer
            {
                NetworkData = new RemotePlayerNetworkData
                {
                    MatchId = matchId,
                    User = userPresence
                }
            };

            SpawnedRemotePlayer?.Invoke(this, new SpawnedPlayerEventArgs(player, userPresence.SessionId));
        }

        // Add the player to the players array.
        Players.Add(userPresence.SessionId, player);
    }

    void RemovePlayer(string sessionId)
    {
        if (!Players.TryGetValue(sessionId, out var player))
            return;

        Players.Remove(sessionId);

        RemovedPlayer?.Invoke(this, new RemovedPlayerEventArgs(sessionId, player));
    }

    /// <summary>
    /// Updates the player's velocity and position based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateTankStateFromState(byte[] state, RemotePlayer networkPlayer)
    {
        //TODO: fix the allocation here
        var packetReader = new PacketReader(new MemoryStream(state));

        var totalSeconds = packetReader.ReadSingle();

        var position = packetReader.ReadVector2();
        var velocity = packetReader.ReadVector2();

        var paddleInput = packetReader.ReadVector2();
        
        ReceivedRemotePlayerPaddleState?.Invoke(
            this,
            new ReceivedRemotePlayerPaddleStateEventArgs(
                totalSeconds,
                position,
                velocity,
                paddleInput,
                networkPlayer.NetworkData.User.SessionId));
    }

    /// <summary>
    /// Updates the ball's direction and position based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateDirectionAndPositionFromState(byte[] state, RemotePlayer networkPlayer)
    {
        var stateDictionary = GetStateAsDictionary(state);

        var direction = float.Parse(stateDictionary["direction"]);

        var position = new Vector2(
            float.Parse(stateDictionary["position.x"]),
            float.Parse(stateDictionary["position.y"]));

        ReceivedRemoteBallState?.Invoke(
            this,
            new ReceivedRemoteBallStateEventArgs(direction, position));
    }

    /// <summary>
    /// Updates the score based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateScoreFromState(byte[] state)
    {
        var stateDictionary = GetStateAsDictionary(state);

        var player1Score = int.Parse(stateDictionary["player1.score"]);
        var player2Score = int.Parse(stateDictionary["player2.score"]);

        ReceivedRemoteScore?.Invoke(
            this,
            new ReceivedRemoteScoreEventArgs(player1Score, player2Score));
    }

    /// <summary>
    /// Converts a byte array of a UTF8 encoded JSON string into a Dictionary.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    /// <returns>A Dictionary containing state data as strings.</returns>
    static IDictionary<string, string> GetStateAsDictionary(byte[] state)
    {
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(state));
    }

    /// <summary>
    /// Sends a match state message across the network.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="state">The stringified JSON state data.</param>
    public async Task SendMatchStateAsync(long opCode, string state)
    {
        await _nakamaConnection.Socket.SendMatchStateAsync(_currentMatch.Id, opCode, state);
    }

    /// <summary>
    /// Sends a match state message across the network.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="state">The stringified JSON state data.</param>
    public void SendMatchState(long opCode, string state)
    {
        _nakamaConnection.Socket.SendMatchStateAsync(_currentMatch.Id, opCode, state);
    }
}
