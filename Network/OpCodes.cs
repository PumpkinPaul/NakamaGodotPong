// Copyright Pumpkin Games Ltd. All Rights Reserved.

namespace NakamaGodotPong.Network;

/// <summary>
/// Defines the various network operations that can be sent/received.
/// </summary>
public class OpCodes
{
    public const long VelocityAndPosition = 1;
    public const long DirectionAndPosition = 2;
    public const long Input = 3;
    public const long Scored = 4;
    public const long Respawned = 5;
    public const long NewRound = 6;
}
