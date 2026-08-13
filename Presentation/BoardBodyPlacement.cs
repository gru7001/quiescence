using System.Collections.Generic;
using Godot;

/// <summary>Board-plane placement for occupants (tile centroids, move lerp).</summary>
public static class BoardBodyPlacement
{
	public static Vector3 OnPlane(
		Body body,
		Dictionary<Tile, TileCoord> tiles,
		float step,
		long now)
	{
		if (TryMoveEndpoints(body, tiles, step, out var from, out var to))
			return from.Lerp(to, MoveProgress(body, now));

		return TryCentroid(body.Occupancy.Occupies(body), tiles, step, out var p) ? p : Vector3.Zero;
	}

	public static float MoveProgress(Body body, long now)
	{
		if (body.ReadActionState() is not Move.InFlightState flight)
			return 0f;
		var span = flight.ArriveAt - flight.StartedAt;
		if (span <= 0)
			return 1f;
		return Mathf.Clamp((now - flight.StartedAt) / (float)span, 0f, 1f);
	}

	public static bool TryMoveEndpoints(
		Body body,
		Dictionary<Tile, TileCoord> tiles,
		float step,
		out Vector3 from,
		out Vector3 to)
	{
		from = default;
		to = default;
		if (body.ReadActionState() is not Move.InFlightState flight)
			return false;

		var hasFrom = TryCentroid(flight.StartTiles, tiles, step, out from);
		var hasTo = TryCentroid(flight.FinalTiles, tiles, step, out to);
		if (!hasFrom && !hasTo)
			return false;
		if (!hasFrom)
			from = to;
		if (!hasTo)
			to = from;
		return true;
	}

	public static bool TryCentroid(
		IEnumerable<Tile> tiles,
		Dictionary<Tile, TileCoord> coords,
		float step,
		out Vector3 position)
	{
		var sx = 0f;
		var sz = 0f;
		var n = 0;
		foreach (var t in tiles)
		{
			if (t == null || !coords.TryGetValue(t, out var c))
				continue;
			sx += c.Col * step;
			sz += c.Row * step;
			n++;
		}
		if (n == 0)
		{
			position = default;
			return false;
		}
		position = new Vector3(sx / n, 0f, sz / n);
		return true;
	}
}
