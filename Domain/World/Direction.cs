using System;

public enum Direction
{
	Up = 0,
	Right = 1,
	Down = 2,
	Left = 3,
}

public static class DirectionExt
{
	public static TileCoord Step(this TileCoord t, Direction d) => d switch
	{
		Direction.Up => new TileCoord(t.Row - 1, t.Col),
		Direction.Right => new TileCoord(t.Row, t.Col + 1),
		Direction.Down => new TileCoord(t.Row + 1, t.Col),
		Direction.Left => new TileCoord(t.Row, t.Col - 1),
		_ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown direction.")
	};
}

