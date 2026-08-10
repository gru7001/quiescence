using System.Collections.Generic;

/// <summary>
/// Two-stage melee punch:
///   wind-up (no world effect) → strike (apply damage to adjacent occupants).
/// World writes for each stage live in that stage's <see cref="IActionState.OnEnter"/>.
/// The wind-up's scheduled event only writes the next state; if the puncher dies during wind-up
/// the dispatch procedure is uninstalled, so the strike state's <c>OnEnter</c> never runs and no
/// damage is applied.
/// </summary>
public static class Punch
{
	public const long WindUpTicks = 4_000L;
	public const int Damage = 4;

	private static readonly Var DirVar = Logic.Var("dir");

	public static readonly CommandDefinition Command = new Command(
		name: "Punch",
		variables: [DirVar],
		static (s, b, a) => Issue(s, b, a),
		constraint: ActionConstraints.BodyIsIdle & ParameterPredicates.Direction[DirVar]);

	/// <summary>
	/// Stage 1. <see cref="OnEnter"/> only schedules the transition to <see cref="StrikeState"/>; no damage yet.
	/// </summary>
	public sealed class WindUpState : IActionState
	{
		/// <summary><see cref="Direction"/> stored as int for save/load.</summary>
		public int Dir;
		public long StrikeAt;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			var dir = Dir;
			var at = StrikeAt;
			scheduler.Schedule(() => body.WriteActionState(new StrikeState { Dir = dir }), at);
		}

		public override string ToString() => $"PunchWindUp(dir={(Direction)Dir}, strikeAt={StrikeAt})";
	}

	/// <summary>
	/// Stage 2. <see cref="OnEnter"/> applies damage to adjacent <see cref="Body"/> occupants and returns to idle.
	/// </summary>
	public sealed class StrikeState : IActionState
	{
		public int Dir;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			ApplyStrike(body, (Direction)Dir);
			body.WriteActionState(IdleActionState.Instance);
		}

		public override string ToString() => $"PunchStrike(dir={(Direction)Dir})";
	}

	private static void Issue(Scheduler scheduler, Body body, Assignment assignment)
	{
		var dir = assignment.Get<Direction>(DirVar);
		body.WriteActionState(new WindUpState
		{
			Dir = (int)dir,
			StrikeAt = scheduler.CurrentTime + WindUpTicks
		});
	}

	private static void ApplyStrike(Body puncher, Direction dir)
	{
		var occ = puncher.Occupancy;
		var hitTiles = new HashSet<Tile>();
		foreach (var t in occ.Occupies(puncher))
		{
			if (t == null)
				continue;
			var e = t.Edge(dir);
			if (e == null || !e.Open || e.To == null)
				continue;
			hitTiles.Add(e.To);
		}

		var damaged = new HashSet<Body>();
		foreach (var tile in hitTiles)
		{
			if (occ.GetAt(tile) is not Body victim)
				continue;
			if (ReferenceEquals(victim, puncher))
				continue;
			if (!damaged.Add(victim))
				continue;
			victim.Resources.AddCur(ResourcesCatalog.Health, -Damage);
		}
	}
}
