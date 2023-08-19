using Godot;
using NakamaGodotPong.Network;
using System.Threading.Tasks;
using System;
using Nakama;
using System.Collections.Generic;
using NakamaGodotPong.Players;

public partial class Game : Node2D
{
    public event EventHandler ExitedMatch;

    public const int SCREEN_WIDTH = 640;
    public const int SCREEN_HEIGHT = 400;

    GodotLogger _logger;

    NetworkGameManager _networkGameManager;

    //Member variables
    Vector2 _screenSize = new(SCREEN_WIDTH, SCREEN_HEIGHT);
    Vector2 _padSize;
    Vector2 _direction = new(1.0f, 0.0f);

    const int INITIAL_BALL_SPEED = 160; //Constant for ball speed (in pixels/second)
    float _ballSpeed = INITIAL_BALL_SPEED; //Speed of the ball (also in pixels/second)
    const int PAD_SPEED = 150; // Constant for pads speed

    const int PLAYER_OFFSET_X = 32;

    readonly Vector2[] _playerSpawnPoints = new[] {
        new Vector2(PLAYER_OFFSET_X, SCREEN_HEIGHT / 2),
        new Vector2(SCREEN_WIDTH - PLAYER_OFFSET_X, SCREEN_HEIGHT / 2)
    };

    int _playerSpawnPointsIdx = 0;
    int _bounceDirection = -1;

    readonly Queue<ReceivedRemotePlayerPaddleStateEventArgs> _networkState = new();

    readonly Dictionary<Player, Paddle> _paddles = new();

    public void SetNetworkGameManager(NetworkGameManager networkGameManager)
    {
        _networkGameManager = networkGameManager;

        _networkGameManager.SpawnedLocalPlayer += OnSpawnedLocalPlayer;
        _networkGameManager.SpawnedRemotePlayer += OnSpawnedRemotePlayer;
        //_networkGameManager.ReceivedRemotePlayerTasnkState += OnReceivedRemotePlayerTankStatePosition;
        _networkGameManager.RemovedPlayer += OnRemovedPlayer;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _logger = new GodotLogger("Nakama", GodotLogger.LogLevel.DEBUG);

        //screen_size = Viewport.Cu _rect().size
        _padSize = GetNode<Sprite2D>("leftPaddle").Texture.GetSize();
        GD.Randomize();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var dt = (float)delta;

        var ballNode = GetNode<Sprite2D>("ball");
        var ballPosition = ballNode.Position;
        var leftRect = new Rect2(GetNode<Sprite2D>("leftPaddle").Position - _padSize * 0.5f, _padSize);
        var rightRect = new Rect2(GetNode<Sprite2D>("rightPaddle").Position - _padSize * 0.5f, _padSize);

        //Integrate new ball position
        ballPosition += _direction * _ballSpeed * dt;

        //Flip when touching roof or floor
        if ((ballPosition.Y < 0 && _direction.Y < 0) || (ballPosition.Y > _screenSize.Y && _direction.Y > 0))
            _direction.Y = -_direction.Y;

        //Flip, change direction and increase speed when touching pads
        if ((leftRect.HasPoint(ballPosition) && _direction.X < 0) || (rightRect.HasPoint(ballPosition) && _direction.Y > 0))
        {
            _direction.X = -_direction.X;
            _direction.Y = GD.Randf() * 2.0f - 1f;
            _direction = _direction.Normalized();
            _ballSpeed *= 1.1f;
        }

        //Check gameover
        if (ballPosition.X < 0 || ballPosition.X > _screenSize.X)
        {
            ballPosition = _screenSize * 0.5f;
            _ballSpeed = INITIAL_BALL_SPEED;
            _direction = new Vector2(-1, 0);
        }

        ballNode.Position = ballPosition;

        //Move left paddle
        var leftPaddleNode = GetNode<Sprite2D>("leftPaddle");
        var leftPosition = leftPaddleNode.Position;

        if (leftPosition.Y > 0 && Input.IsActionPressed("left_move_up"))
            leftPosition.Y += -PAD_SPEED * dt;
        else if (leftPosition.Y < _screenSize.Y && Input.IsActionPressed("left_move_down"))
            leftPosition.Y += PAD_SPEED * dt;

        leftPaddleNode.Position = leftPosition;

        //Move right paddle
        var rightPaddleNode = GetNode<Sprite2D>("rightPaddle");
        var rightPosition = rightPaddleNode.Position;

        if (rightPosition.Y > 0 && Input.IsActionPressed("right_move_up"))
            rightPosition.Y += -PAD_SPEED * dt;
        else if (rightPosition.Y < _screenSize.Y && Input.IsActionPressed("right_move_down"))
            rightPosition.Y += PAD_SPEED * dt;

        rightPaddleNode.Position = rightPosition;
    }

    /// <summary>
    /// Quits the current match.
    /// </summary>
    public async Task QuitMatch()
    {
        _logger.DebugFormat($"PlayGamePhase.QuitMatch");

        await _networkGameManager.QuitMatch();

        ExitedMatch?.Invoke(this, EventArgs.Empty);
    }

    void OnSpawnedLocalPlayer(object sender, SpawnedPlayerEventArgs e)
    {
        var position = _playerSpawnPoints[_playerSpawnPointsIdx];

        _logger.DebugFormat($"PlayGamePhase.OnSpawnedLocalPlayer - create local paddle at position: {position}");
        _paddles[e.Player] = new Paddle(position, _screenSize);

        PrepareNextPlayer();
    }

    void OnSpawnedRemotePlayer(object sender, SpawnedPlayerEventArgs e)
    {
        var position = _playerSpawnPoints[_playerSpawnPointsIdx];

        _logger.DebugFormat($"PlayGamePhase.OnSpawnedRemotePlayer - create remote paddle at position: {position}");
        _paddles[e.Player] = new Paddle(position, _screenSize);

        PrepareNextPlayer();
    }

    void PrepareNextPlayer()
    {
        //Cycle through the spawn points so that players are located in the correct postions and flipping the bounce direction
        _playerSpawnPointsIdx = (_playerSpawnPointsIdx + 1) % _playerSpawnPoints.Length;
        _bounceDirection = -_bounceDirection;
    }

    void OnReceivedRemotePlayerTankStatePosition(object sender, ReceivedRemotePlayerPaddleStateEventArgs e)
    {
        _networkState.Enqueue(e);
    }

    void OnRemovedPlayer(object sender, RemovedPlayerEventArgs e)
    {
        _paddles.Remove(e.Player);
    }
}
