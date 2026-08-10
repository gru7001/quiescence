public static class Fireball
{
	private static readonly Var TargetVar = Logic.Var("target");

	private static readonly Proposition HasPyromancer = Logic.Proposition(b =>
		b.Perks.Has(PerksCatalog.Pyromancer));

	/// <summary>Cast a fireball at a target tile. Requires the Pyromancer perk.</summary>
	public static readonly CommandDefinition Command = new Command(
		name: "Cast Fireball",
		variables: [TargetVar],
		static (s, b, a) => Issue(s, b, a),
		constraint: ActionConstraints.BodyIsIdle & HasPyromancer & ParameterPredicates.Tile[TargetVar]);

	public const long CastDurationTicks = 5_000L;

	/// <summary>
	/// Single-stage cast. Currently has no world effect; <see cref="OnEnter"/> only schedules return-to-idle.
	/// </summary>
	public sealed class InFlightState : IActionState
	{
		public Tile Target;
		public long CompleteAt;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			scheduler.Schedule(() => body.WriteActionState(IdleActionState.Instance), CompleteAt);
		}
	}

	private static void Issue(Scheduler scheduler, Body caster, Assignment assignment)
	{
		var target = assignment.Get<Tile>(TargetVar);
		caster.WriteActionState(new InFlightState
		{
			Target = target,
			CompleteAt = scheduler.CurrentTime + CastDurationTicks
		});
	}
}
