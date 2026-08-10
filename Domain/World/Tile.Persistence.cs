public static class TilePersistence
{
	public const string SaveSchemaId = "tile.v1";

	public sealed record TileSave(
		int Group,
		NodeRef Up,
		NodeRef Right,
		NodeRef Down,
		NodeRef Left);

	public static TileSave Encode(Tile t, SaveSession session)
	{
		return new TileSave(
			Group: t.Group.Value,
			Up: session.Ref(t.Up),
			Right: session.Ref(t.Right),
			Down: session.Ref(t.Down),
			Left: session.Ref(t.Left));
	}

	public static void Apply(Tile t, TileSave save, LoadSession session)
	{
		t.SetGroup(new GroupId(save.Group));
		t.SetEdges(
			up: (Edge)session.Ref(save.Up),
			right: (Edge)session.Ref(save.Right),
			down: (Edge)session.Ref(save.Down),
			left: (Edge)session.Ref(save.Left));
	}
}

