public static class EdgePersistence
{
	public const string SaveSchemaId = "edge.v1";

	public sealed record EdgeSave(
		NodeRef From,
		Direction Dir,
		NodeRef To,
		bool Open);

	public static EdgeSave Encode(Edge e, SaveSession session)
	{
		return new EdgeSave(
			From: session.Ref(e.From),
			Dir: e.Dir,
			To: session.RefOrNull(e.To),
			Open: e.Open);
	}

	public static void Apply(Edge e, EdgeSave save, LoadSession session)
	{
		e.SetTo((Tile)session.Ref(save.To));
		e.SetOpen(save.Open);
	}
}

