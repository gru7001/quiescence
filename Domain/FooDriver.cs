using System;
using System.Collections.Generic;

/// <summary>
/// Simple AI driver: BFS for the nearest other <see cref="Body"/> within <see cref="ScanRadius"/>;
/// walks toward it with <see cref="Commands.Move"/>, punches when adjacent, otherwise waits.
/// </summary>
public sealed class FooDriver : IDriver, ISaveable<FooDriverPersistence.DriverSave>
{
	/// <summary>Max graph distance (edges) from any tile the vehicle occupies.</summary>
	public const int ScanRadius = 48;

	public long WaitDeltaTicks { get; }

	public FooDriver(long waitDeltaTicks = 5000) => WaitDeltaTicks = waitDeltaTicks;

	public void OnDecisionNeeded(Body vehicle, Func<CommandDefinition, Assignment, bool> submit)
	{
		if (TryFindNearestOtherBody(vehicle, out var dist, out var firstStep))
		{
			if (dist == 1)
			{
				if (Punch.Command.TryBindVariables(firstStep, out var punchA) && submit(Punch.Command, punchA))
					return;
				if (Move.Command.TryBindVariables(firstStep, out var moveA) && submit(Move.Command, moveA))
					return;
			}
			else
			{
				if (Move.Command.TryBindVariables(firstStep, out var moveA2) && submit(Move.Command, moveA2))
					return;
			}
		}

		if (Commands.Wait.TryBindVariables(WaitDeltaTicks, out var waitA))
			submit(Commands.Wait, waitA);
	}

	/// <summary>
	/// Shortest-path over open edges; non-body occupants block; own footprint is traversable.
	/// Neighbor order is Up, Right, Down, Left — first hit at minimum distance wins.
	/// </summary>
	private static bool TryFindNearestOtherBody(Body vehicle, out int distance, out Direction firstStep)
	{
		distance = 0;
		firstStep = default;

		var occ = vehicle.Occupancy;
		var q = new Queue<(Tile Tile, int Dist, Direction First)>();
		var seen = new HashSet<Tile>();

		foreach (var t in occ.Occupies(vehicle))
		{
			if (t != null && seen.Add(t))
				q.Enqueue((t, 0, default));
		}

		while (q.Count > 0)
		{
			var (cur, dist, fs) = q.Dequeue();
			if (dist >= ScanRadius)
				continue;

			for (var i = 0; i < NeighborDirs.Length; i++)
			{
				var dir = NeighborDirs[i];
				var e = cur.Edge(dir);
				if (e == null || !e.Open || e.To == null)
					continue;

				var next = e.To;
				var at = occ.GetAt(next);
				var newDist = dist + 1;
				var newFs = dist == 0 ? dir : fs;

				if (at is Body b && !ReferenceEquals(b, vehicle))
				{
					distance = newDist;
					firstStep = newFs;
					return true;
				}

				if (at != null && !ReferenceEquals(at, vehicle))
					continue;

				if (!seen.Add(next))
					continue;

				q.Enqueue((next, newDist, newFs));
			}
		}

		return false;
	}

	private static readonly Direction[] NeighborDirs =
	{
		Direction.Up,
		Direction.Right,
		Direction.Down,
		Direction.Left,
	};

	public SaveNode<FooDriverPersistence.DriverSave> SaveTo(SaveSession session) =>
		new(FooDriverPersistence.SaveSchemaId, FooDriverPersistence.Encode(this));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}
