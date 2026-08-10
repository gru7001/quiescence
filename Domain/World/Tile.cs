using System;

public sealed class Tile : ISaveable<TilePersistence.TileSave>
{
	public GroupId Group { get; private set; }
	public Edge Up { get; private set; }
	public Edge Right { get; private set; }
	public Edge Down { get; private set; }
	public Edge Left { get; private set; }

	public Tile(GroupId group)
	{
		Group = group;
	}

	public void SetEdges(Edge up, Edge right, Edge down, Edge left)
	{
		Up = up;
		Right = right;
		Down = down;
		Left = left;
	}

	public void SetGroup(GroupId group) => Group = group;

	public Edge Edge(Direction d) => d switch
	{
		Direction.Up => Up,
		Direction.Right => Right,
		Direction.Down => Down,
		Direction.Left => Left,
		_ => null
	};

	public SaveNode<TilePersistence.TileSave> SaveTo(SaveSession session) =>
		new(TilePersistence.SaveSchemaId, TilePersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}

