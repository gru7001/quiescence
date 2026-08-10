public static class Wait
{
	private static readonly Var DeltaTicksVar = Logic.Var("deltaTicks");

	/// <summary>Same instance as in <see cref="Command"/>; used for derived-formula long-parameter checks.</summary>
	public static readonly Predicate<long> NonNegativeTicks = Logic.Predicate<long>((_, d) => d >= 0);

	/// <summary>Wait for <c>deltaTicks</c> from now (<see cref="long"/> argument).</summary>
	public static readonly CommandDefinition Command = new Command(
		name: "Wait",
		variables: [DeltaTicksVar],
		static (s, b, a) => Issue(s, b, a),
		constraint: ActionConstraints.BodyIsIdle & ParameterPredicates.Long[DeltaTicksVar] & NonNegativeTicks[DeltaTicksVar]);

	/// <summary>
	/// Single-stage wait. <see cref="OnEnter"/> schedules a state-write back to <see cref="IdleActionState"/>
	/// at <see cref="DueAt"/>; no world writes occur from the scheduled callback.
	/// </summary>
	public sealed class DueTimeState : IActionState
	{
		public long DueAt;

		public void OnEnter(Scheduler scheduler, Body body)
		{
			scheduler.Schedule(() => body.WriteActionState(IdleActionState.Instance), DueAt);
		}

		public override string ToString() => $"Wait(dueAt={DueAt})";
	}

	private static void Issue(Scheduler scheduler, Body body, Assignment assignment)
	{
		var deltaTicks = assignment.Get<long>(DeltaTicksVar);
		body.WriteActionState(new DueTimeState { DueAt = scheduler.CurrentTime + deltaTicks });
	}
}
