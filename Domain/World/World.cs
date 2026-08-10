using System.Collections.Generic;

/// <summary>
/// Tile-centric world graph. Tiles own adjacency (4-way neighbor links).
/// GroupId is purely a display/partition label; it does not own connectivity.
/// </summary>
public sealed class World : ISaveable<WorldPersistence.WorldSave>
{
	public readonly ExecutionContext Ctx;

	public World(ExecutionContext ctx) => Ctx = ctx;

	private readonly Key<List<Tile>> _tiles = new();

	public IReadOnlyList<Tile> ReadTiles() => Ctx.Read(_tiles) ?? new List<Tile>();

	public void WriteTiles(List<Tile> tiles) => Ctx.Write(_tiles, tiles);

	public bool TryNeighbor(Tile from, Direction dir, out Tile dest)
	{
		var e = from.Edge(dir);
		if (e == null || !e.Open)
		{
			dest = null;
			return false;
		}
		dest = e.To;
		return dest != null;
	}

	public SaveNode<WorldPersistence.WorldSave> SaveTo(SaveSession session) =>
		new(WorldPersistence.SaveSchemaId, WorldPersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}

public readonly record struct GroupId(int Value);

public static class WorldLayout
{
	private static (int dRow, int dCol) Delta(Direction d) => d switch
	{
		Direction.Up => (-1, 0),
		Direction.Right => (0, 1),
		Direction.Down => (1, 0),
		Direction.Left => (0, -1),
		_ => (0, 0)
	};

	/// <summary>
	/// Derive tile coordinates for one display group by walking the neighbor relation.
	/// The resulting coordinates are unique up to translation if the graph is grid-consistent.
	/// </summary>
	// Layout derivation requires edges; use the overload below.

	/// <summary>Layout derivation that can follow edges to neighbor tiles.</summary>
	public static Dictionary<Tile, TileCoord> DeriveGroupCoords(IReadOnlyList<Tile> tiles, GroupId group)
	{
		var coords = new Dictionary<Tile, TileCoord>();
		if (tiles == null || tiles.Count == 0)
			return coords;

		Tile root = null;
		for (var i = 0; i < tiles.Count; i++)
		{
			if (tiles[i].Group.Equals(group))
			{
				root = tiles[i];
				break;
			}
		}
		if (root == null)
			return coords;

		var q = new Queue<Tile>();
		coords[root] = new TileCoord(0, 0);
		q.Enqueue(root);

		while (q.Count > 0)
		{
			var u = q.Dequeue();
			var uCoord = coords[u];

			void Visit(Direction d, Edge e)
			{
				if (e == null)
					return;
				var v = e.To;
				if (v == null || !v.Group.Equals(group))
					return;

				var (dr, dc) = Delta(d);
				var want = new TileCoord(uCoord.Row + dr, uCoord.Col + dc);

				if (coords.TryGetValue(v, out var have))
				{
					if (!have.Equals(want))
						return;
					return;
				}

				coords[v] = want;
				q.Enqueue(v);
			}

			Visit(Direction.Up, u.Up);
			Visit(Direction.Right, u.Right);
			Visit(Direction.Down, u.Down);
			Visit(Direction.Left, u.Left);
		}

		return coords;
	}
}

