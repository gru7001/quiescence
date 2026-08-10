using System;
using System.Collections.Generic;

/// <summary>Something that can occupy positions.</summary>
public interface IOccupant { }

/// <summary>
/// Primitive spatial fact: an <see cref="IOccupant"/> occupies a <see cref="Tile"/>.
/// No stacking: each tile has at most one occupant.
/// </summary>
public sealed class Occupancy : ISaveable<OccupancyPersistence.OccupancySave>
{
	public readonly ExecutionContext Ctx;

	private readonly Key<Dictionary<IOccupant, HashSet<Tile>>> _byOccupant = new();
	private readonly Key<Dictionary<Tile, IOccupant>> _byTile = new();

	public Occupancy(ExecutionContext ctx) => Ctx = ctx;

	public void ClearAll()
	{
		Ctx.Write(_byOccupant, new Dictionary<IOccupant, HashSet<Tile>>());
		Ctx.Write(_byTile, new Dictionary<Tile, IOccupant>());
	}

	public IEnumerable<(IOccupant Occupant, Tile Tile)> Entries()
	{
		foreach (var kv in ReadByTile())
			yield return (kv.Value, kv.Key);
	}

	public SaveNode<OccupancyPersistence.OccupancySave> SaveTo(SaveSession session) =>
		new(OccupancyPersistence.SaveSchemaId, OccupancyPersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();

	Dictionary<IOccupant, HashSet<Tile>> ReadByOccupant() =>
		Ctx.Read(_byOccupant) ?? new Dictionary<IOccupant, HashSet<Tile>>();

	Dictionary<Tile, IOccupant> ReadByTile() =>
		Ctx.Read(_byTile) ?? new Dictionary<Tile, IOccupant>();

	public bool TryAdd(IOccupant e, Tile t)
	{
		var byTile0 = ReadByTile();
		if (byTile0.TryGetValue(t, out var existing) && !ReferenceEquals(existing, e))
			return false;

		var byOcc0 = ReadByOccupant();
		var byOcc = new Dictionary<IOccupant, HashSet<Tile>>(byOcc0);
		if (!byOcc.TryGetValue(e, out var ps0))
			ps0 = new HashSet<Tile>();
		var ps = new HashSet<Tile>(ps0);
		if (!ps.Add(t))
			return true;
		byOcc[e] = ps;

		var byTile = new Dictionary<Tile, IOccupant>(byTile0) { [t] = e };
		Ctx.Write(_byOccupant, byOcc);
		Ctx.Write(_byTile, byTile);
		return true;
	}

	public void Remove(IOccupant e, Tile t)
	{
		var byOcc0 = ReadByOccupant();
		if (!byOcc0.TryGetValue(e, out var ps0) || !ps0.Contains(t))
			return;

		var byTile0 = ReadByTile();
		if (!byTile0.TryGetValue(t, out var existing) || !ReferenceEquals(existing, e))
			return;

		var ps = new HashSet<Tile>(ps0);
		ps.Remove(t);
		var byOcc = new Dictionary<IOccupant, HashSet<Tile>>(byOcc0);
		if (ps.Count == 0)
			byOcc.Remove(e);
		else
			byOcc[e] = ps;

		var byTile = new Dictionary<Tile, IOccupant>(byTile0);
		byTile.Remove(t);

		Ctx.Write(_byOccupant, byOcc);
		Ctx.Write(_byTile, byTile);
	}

	public IEnumerable<Tile> Occupies(IOccupant e) =>
		ReadByOccupant().TryGetValue(e, out var ps) ? ps : Array.Empty<Tile>();

	public IOccupant GetAt(Tile t) =>
		ReadByTile().TryGetValue(t, out var e) ? e : null;

	public T GetAt<T>(Tile t) where T : class, IOccupant =>
		GetAt(t) as T;

	public bool TrySetPositions(IOccupant e, IReadOnlyCollection<Tile> tiles)
	{
		if (tiles.Count == 0)
			return false;

		var byTile0 = ReadByTile();

		foreach (var t in tiles)
			if (byTile0.TryGetValue(t, out var existing) && !ReferenceEquals(existing, e))
				return false;

		var byOcc0 = ReadByOccupant();
		byOcc0.TryGetValue(e, out var ps0);
		ps0 ??= new HashSet<Tile>();

		var byTile = new Dictionary<Tile, IOccupant>(byTile0);
		foreach (var t in ps0)
			if (byTile.TryGetValue(t, out var existing) && ReferenceEquals(existing, e))
				byTile.Remove(t);

		var ps = new HashSet<Tile>(tiles);
		foreach (var t in ps)
			byTile[t] = e;

		var byOcc = new Dictionary<IOccupant, HashSet<Tile>>(byOcc0) { [e] = ps };

		Ctx.Write(_byOccupant, byOcc);
		Ctx.Write(_byTile, byTile);
		return true;
	}

	public void SetPositions(IOccupant e, IReadOnlyCollection<Tile> tiles)
	{
		if (!TrySetPositions(e, tiles))
			throw new InvalidOperationException("Occupancy.SetPositions: footprint conflict.");
	}
}

