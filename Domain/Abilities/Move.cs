using System;
using System.Collections.Generic;

/// <summary>
/// Two-stage move: in-flight (start ∪ dest occupancy) → arrived (final-only occupancy).
/// World writes for each stage happen in that stage's <see cref="IActionState.OnEnter"/>; the
/// scheduled wind-down event only writes the next state, so a death between issue and arrival
/// neutralises the arrival because the dispatch procedure has been uninstalled.
/// </summary>
public static class Move
{
	private static readonly Var DirVar = Logic.Var("dir");

	public static readonly CommandDefinition Command = new Command(
		name: "Move",
		variables: [DirVar],
		static (s, b, a) => Issue(s, b, a),
		constraint: ActionConstraints.BodyIsIdle
			& ParameterPredicates.Direction[DirVar]
			& OpenNeighbor.Vacant[DirVar]);

	public const long TicksPerUnitDistance = 10_000L;

	/// <summary>
	/// Stage 1. Body occupies start ∪ final until <see cref="ArriveAt"/>; on entry, schedules a state-write
	/// to <see cref="ArrivedState"/>. No world writes are performed from the scheduled callback.
	/// </summary>
	public sealed class InFlightState : IActionState
	{
		public IReadOnlyCollection<Tile> StartTiles = null!;
		public IReadOnlyCollection<Tile> FinalTiles = null!;
		public long StartedAt;
		public long ArriveAt;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			var final = FinalTiles;
			scheduler.Schedule(() => body.WriteActionState(new ArrivedState { FinalTiles = final }), ArriveAt);
		}
	}

	/// <summary>
	/// Stage 2. On entry, collapses occupancy to <see cref="FinalTiles"/> and returns to idle.
	/// </summary>
	public sealed class ArrivedState : IActionState
	{
		public IReadOnlyCollection<Tile> FinalTiles = null!;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			body.Occupancy.SetPositions(body, FinalTiles);
			body.WriteActionState(IdleActionState.Instance);
		}
	}

	private static void Issue(Scheduler scheduler, Body body, Assignment assignment)
	{
		var dir = assignment.Get<Direction>(DirVar);
		OpenNeighbor.TryGet(body, dir, out var dest);
		var start = new HashSet<Tile>(body.Occupancy.Occupies(body));
		var final = new HashSet<Tile> { dest };
		var union = new HashSet<Tile>(start);
		union.UnionWith(final);
		body.Occupancy.SetPositions(body, union);

		var speed = body.Stats.Read(StatsCatalog.MoveSpeed);
		var durationTicks = Math.Max(1L, (long)Math.Ceiling((double)TicksPerUnitDistance / speed));
		var startedAt = scheduler.CurrentTime;
		body.WriteActionState(new InFlightState
		{
			StartTiles = start,
			FinalTiles = final,
			StartedAt = startedAt,
			ArriveAt = startedAt + durationTicks
		});
	}
}
