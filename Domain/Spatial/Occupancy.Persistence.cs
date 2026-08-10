using System.Collections.Generic;

public static class OccupancyPersistence
{
	public const string SaveSchemaId = "occupancy.v1";

	public sealed record OccupantPositionsSave(
		NodeRef Occupant,
		IReadOnlyList<NodeRef> Tiles);

	public sealed record OccupancySave(
		IReadOnlyList<OccupantPositionsSave> Occupants);

	public static OccupancySave Encode(Occupancy occ, SaveSession session)
	{
		var list = new List<OccupantPositionsSave>();
		var byOcc = new Dictionary<IOccupant, List<NodeRef>>();

		foreach (var (occant, t) in occ.Entries())
		{
			if (!byOcc.TryGetValue(occant, out var ps))
			{
				ps = new List<NodeRef>();
				byOcc.Add(occant, ps);
			}

			ps.Add(session.Ref(t));
		}

		foreach (var (occant, ps) in byOcc)
		{
			if (occant is not ISaveable saveable)
				continue;

			var occRef = session.Ref(saveable);
			list.Add(new OccupantPositionsSave(
				Occupant: occRef,
				Tiles: ps));
		}

		return new OccupancySave(Occupants: list);
	}

	public static void Apply(Occupancy occ, OccupancySave save, LoadSession session)
	{
		occ.ClearAll();
		foreach (var o in save.Occupants)
		{
			var occupant = (IOccupant)session.Ref(o.Occupant);
			foreach (var t in o.Tiles)
				occ.TryAdd(occupant, (Tile)session.Ref(t));
		}
	}
}

