using System.Collections.Generic;

public static class WorldPersistence
{
	public const string SaveSchemaId = "world.v1";

	public sealed record WorldSave(IReadOnlyList<NodeRef> Tiles);

	public static WorldSave Encode(World world, SaveSession session)
	{
		var tiles = world.ReadTiles();
		var list = new List<NodeRef>(tiles.Count);
		for (var i = 0; i < tiles.Count; i++)
			list.Add(session.Ref(tiles[i]));
		return new WorldSave(list);
	}

	public static void Apply(World world, WorldSave save, LoadSession session)
	{
		var tiles = new List<Tile>(save.Tiles.Count);
		for (var i = 0; i < save.Tiles.Count; i++)
			tiles.Add((Tile)session.Ref(save.Tiles[i]));
		world.WriteTiles(tiles);
	}
}

