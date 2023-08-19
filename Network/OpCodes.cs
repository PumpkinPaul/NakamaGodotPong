// Copyright Pumpkin Games Ltd. All Rights Reserved.

namespace NakamaGodotPong.Network;

/// <summary>
/// Defines the various network operations that can be sent/received.
/// </summary>
public class OpCodes
{
    public const long PADDLE_PACKET = 1;
    public const long BALL_PACKET = 2;
    public const long SCORED = 3;
    public const long RESPAWNED = 4;
    public const long NEW_ROUND = 5;
}
