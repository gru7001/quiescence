/// <summary>
/// Open adjacency from a body's occupied footprint in a direction:
/// an open edge whose far tile is the neighbor.
/// </summary>
public static class OpenNeighbor
{
	/// <summary>Neighbor through an open edge is vacant or occupied only by the same body.</summary>
	public static readonly Predicate<Direction> Vacant = Logic.Predicate<Direction>(VacantImpl);

	public static bool TryGet(Body body, Direction dir, out Tile neighbor)
	{
		neighbor = null;
		Tile start = null;
		foreach (var t in body.Occupancy.Occupies(body))
			start = t;
		var e = start.Edge(dir);
		if (e == null || !e.Open)
			return false;
		neighbor = e.To;
		return neighbor != null;
	}

	private static bool VacantImpl(Body body, Direction dir)
	{
		if (!TryGet(body, dir, out var neighbor))
			return false;
		var occ = body.Occupancy.GetAt(neighbor);
		return occ == null || ReferenceEquals(occ, body);
	}
}
