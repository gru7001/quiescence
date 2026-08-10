using System;
using System.Collections.Generic;

/// <summary>
/// Root aggregate for traversal-based save/load and the facade for body-related runtime wiring.
/// Lifecycle responsibilities owned here:
/// - install per-body runtime procedures (action dispatch + death watcher) in <see cref="SetupRuntime"/>
/// - install decision-system couplings via <see cref="RegisterCoupling"/>
/// - tear down all of the above on death via <see cref="OnBodyDeath"/>
/// Only living bodies are present in <see cref="Bodies"/>; on death a body is corpsified and removed.
/// </summary>
public sealed class Game : ISaveable<GamePersistence.GameSave>
{
	public Clock Clock { get; }
	public Occupancy Occupancy { get; }
	public DecisionObligations Obligations { get; private set; } = null!;
	public World World { get; }
	public IReadOnlyList<Body> Bodies => _bodies;

	private readonly List<Body> _bodies;

	/// <summary>
	/// Runtime-only handle storage for the per-body action-dispatch procedure. Not serialized;
	/// rebuilt by <see cref="SetupRuntime"/>.
	/// </summary>
	private readonly Dictionary<Body, ProcedureHandle> _dispatchHandles = new();

	public Game(Clock clock, Occupancy occupancy, World world, DecisionObligations obligations, List<Body> bodies)
	{
		Clock = clock ?? throw new ArgumentNullException(nameof(clock));
		Occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
		World = world ?? throw new ArgumentNullException(nameof(world));
		Obligations = obligations ?? throw new ArgumentNullException(nameof(obligations));
		_bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
	}

	public Game(Clock clock, Occupancy occupancy, World world, DecisionObligations obligations)
		: this(clock, occupancy, world, obligations, new List<Body>()) { }

	public SaveNode<GamePersistence.GameSave> SaveTo(SaveSession session) =>
		new(GamePersistence.SaveSchemaId, GamePersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();

	internal void ReplaceRoots(List<Body> bodies)
	{
		_bodies.Clear();
		_bodies.AddRange(bodies);
	}

	internal void SetObligations(DecisionObligations obligations) =>
		Obligations = obligations ?? throw new ArgumentNullException(nameof(obligations));

	/// <summary>
	/// Installs runtime procedures (action dispatch + death watcher per body, decision-system per-pair procedures).
	/// Not serialized; call after construction (new game) or after <see cref="LoadSession.Drain"/> (load).
	/// </summary>
	public static void SetupRuntime(Game game, Scheduler scheduler)
	{
		foreach (var body in game._bodies)
		{
			var dispatch = ActionDispatch.InstallDispatchProcedure(scheduler, body);
			game._dispatchHandles[body] = dispatch;
			InstallDeathWatcher(scheduler, game, body);
		}
		game.Obligations.Setup(scheduler);
	}

	/// <summary>
	/// Couples a driver to a vehicle (delegates to <see cref="DecisionObligations.RegisterCoupling"/>).
	/// Must be called inside a <see cref="Scheduler.RunScoped(System.Action)"/> scope (sim write).
	/// Procedures are installed later by <see cref="SetupRuntime"/>; teardown happens in <see cref="OnBodyDeath"/>.
	/// </summary>
	public void RegisterCoupling(IDriver driver, Body vehicle) =>
		Obligations.RegisterCoupling(driver, vehicle);

	/// <summary>
	/// Single point that tears down all body-bound runtime wiring on death:
	/// action dispatch, decision-system couplings, removal from <see cref="Bodies"/>,
	/// and the body-level <see cref="Body.Corpsify"/>.
	/// Called from the death watcher installed by <see cref="SetupRuntime"/>.
	/// </summary>
	internal void OnBodyDeath(Scheduler scheduler, Body body)
	{
		if (_dispatchHandles.TryGetValue(body, out var dispatch))
		{
			scheduler.RemoveProcedure(dispatch);
			_dispatchHandles.Remove(body);
		}
		Obligations.DecoupleAllForBody(scheduler, body);
		_bodies.Remove(body);
		body.Corpsify();
	}

	/// <summary>
	/// One per-body procedure that fires once on the alive→dead edge:
	/// invokes <see cref="OnBodyDeath"/> and removes itself.
	/// No memo: self-removal makes the procedure fire-once.
	/// </summary>
	private static void InstallDeathWatcher(Scheduler scheduler, Game game, Body body)
	{
		ProcedureHandle handle = default;
		handle = scheduler.AddProcedure(() =>
		{
			if (!body.IsDead())
				return;
			game.OnBodyDeath(scheduler, body);
			scheduler.RemoveProcedure(handle);
		});
	}
}
