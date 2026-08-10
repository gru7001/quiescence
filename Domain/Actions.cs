/// <summary>
/// Per-stage action state. Each implementation is a state a <see cref="Body"/> can be in.
/// The dispatcher (see <see cref="ActionDispatch"/>) calls <see cref="OnEnter"/> when this
/// state becomes the body's current action state.
///
/// Convention:
/// - World writes for a stage happen in <see cref="OnEnter"/>.
/// - Time-driven progression is implemented by scheduling an event whose only effect is to
///   write the next state on the body. The next state's <see cref="OnEnter"/> performs that
///   stage's world writes. This makes late timer firings inert when the per-body dispatch
///   procedure is uninstalled (e.g. on death).
/// </summary>
public interface IActionState
{
	void OnEnter(Scheduler scheduler, Body body);
}

/// <summary>
/// "No action" state. Singleton; <see cref="OnEnter"/> is a no-op.
/// </summary>
public sealed class IdleActionState : IActionState
{
	public static readonly IdleActionState Instance = new();
	private IdleActionState() { }
	public void OnEnter(Scheduler scheduler, Body body) { }
	public override string ToString() => "Idle";
}

/// <summary>Shared constraint atoms for <see cref="IActionState"/>.</summary>
public static class ActionConstraints
{
	/// <summary><see cref="Body.ReadActionState"/> is <see cref="IdleActionState"/>.</summary>
	public static readonly Proposition BodyIsIdle = Logic.Proposition(static b =>
		b.ReadActionState() is IdleActionState);
}

/// <summary>
/// Per-body change-triggered procedure: when <see cref="Body.ActionState"/> changes, run the
/// new state's <see cref="IActionState.OnEnter"/>. Installed at runtime; not serialized.
/// </summary>
public static class ActionDispatch
{
	public static ProcedureHandle InstallDispatchProcedure(Scheduler scheduler, Body body) =>
		ProcedurePatterns.ChangeTriggered(
			scheduler,
			body.ActionState,
			onChange: (s, current, _) => current?.OnEnter(s, body));
}
