public sealed class Edge : ISaveable<EdgePersistence.EdgeSave>
{
	public Tile From { get; private set; }
	public Direction Dir { get; private set; }
	public Tile To { get; private set; }
	public bool Open { get; private set; }

	public Edge(Tile from, Direction dir, Tile to, bool open = true)
	{
		From = from;
		Dir = dir;
		To = to;
		Open = open;
	}

	public void SetTo(Tile to) => To = to;
	public void SetOpen(bool open) => Open = open;

	public SaveNode<EdgePersistence.EdgeSave> SaveTo(SaveSession session) =>
		new(EdgePersistence.SaveSchemaId, EdgePersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}

