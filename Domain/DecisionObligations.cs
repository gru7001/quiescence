using System.Collections.Generic;

public readonly record struct DecisionObligation(IDriver Driver, Body Vehicle);

/// <summary>
/// Owns decision obligation keys for one simulation (<see cref="ExecutionContext"/>).
/// <see cref="RegisteredCouplings"/> is saved as <see cref="NodeRef"/> to driver and body (<see cref="IDriver"/> is <see cref="ISaveable"/>).
/// </summary>
public sealed class DecisionObligations : ISaveable<DecisionObligationsPersistence.ObligationsSave>
{
	private readonly ExecutionContext _ctx;

	/// <summary>Same context used by the sim <see cref="Scheduler"/> for this session.</summary>
	public ExecutionContext Ctx => _ctx;

	public DecisionObligations(ExecutionContext ctx) => _ctx = ctx;

	/// <summary>Maintains <c>U</c>: (d,v) ∈ U iff DecisionPoint(v,S). Derived at runtime; typically not persisted.</summary>
	public readonly Key<HashSet<DecisionObligation>> U = new();

	/// <summary>Driver–vehicle pairs that receive per-pair maintenance procedures; the savable coupling set.</summary>
	public readonly Key<HashSet<DecisionObligation>> RegisteredCouplings = new();

	/// <summary>
	/// Runtime handles for the per-pair procedures; not serialized. Rebuilt by <see cref="Setup"/> after load.
	/// </summary>
	private readonly Dictionary<DecisionObligation, (ProcedureHandle Maintenance, ProcedureHandle[] Observations)> _handles = new();

	public SaveNode<DecisionObligationsPersistence.ObligationsSave> SaveTo(SaveSession session) =>
		new(DecisionObligationsPersistence.SaveSchemaId, DecisionObligationsPersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();

	/// <summary>
	/// One procedure per pair: keeps <see cref="U"/> in sync with <c>DecisionPoint(vehicle)</c> for that pair only.
	/// </summary>
	public ProcedureHandle AddObligationMaintenanceProcedure(Scheduler s, DecisionObligation ob) =>
		s.AddProcedure(() =>
		{
			var u = _ctx.Read(U) ?? new HashSet<DecisionObligation>();
			var want = Commands.DecisionPoint(ob.Vehicle);
			var has = u.Contains(ob);
			if (want == has)
				return;
			var u2 = new HashSet<DecisionObligation>(u);
			if (want)
				u2.Add(ob);
			else
				u2.Remove(ob);
			_ctx.Write(U, u2);
		});

	/// <summary>
	/// Adds <paramref name="driver"/>/<paramref name="vehicle"/> to <see cref="RegisteredCouplings"/>.
	/// Per-pair runtime procedures are installed later by <see cref="Setup"/> (called from <see cref="Game.SetupRuntime"/>).
	/// No-op if the pair is already registered. Must be called from a context where writing to the sim is allowed.
	/// </summary>
	public void RegisterCoupling(IDriver driver, Body vehicle)
	{
		var ob = new DecisionObligation(driver, vehicle);
		var c = _ctx.Read(RegisteredCouplings) ?? new HashSet<DecisionObligation>();
		if (c.Contains(ob))
			return;
		var c2 = new HashSet<DecisionObligation>(c) { ob };
		_ctx.Write(RegisteredCouplings, c2);
	}

	/// <summary>
	/// Installs maintenance + one procedure per <see cref="IDriver.SimObservations"/> entry for each registered pair.
	/// Single install path; called once from <see cref="Game.SetupRuntime"/> after data (new game writes or load drain) is in place.
	/// </summary>
	public void Setup(Scheduler s)
	{
		var pairs = _ctx.Read(RegisteredCouplings);
		if (pairs == null || pairs.Count == 0)
			return;
		foreach (var ob in pairs)
			InstallProceduresFor(s, ob);
	}

	/// <summary>
	/// Removes every coupling whose <see cref="DecisionObligation.Vehicle"/> is <paramref name="body"/>:
	/// drops them from <see cref="RegisteredCouplings"/> and from <see cref="U"/>, and tears down their per-pair procedures.
	/// Intended to be called from a procedure run already inside an <see cref="ExecutionContext.Record"/> scope
	/// (e.g. the death watcher in <see cref="Game.SetupRuntime"/>).
	/// </summary>
	public void DecoupleAllForBody(Scheduler s, Body body)
	{
		var pairs = _ctx.Read(RegisteredCouplings);
		if (pairs == null || pairs.Count == 0)
			return;

		List<DecisionObligation> removed = null;
		foreach (var ob in pairs)
		{
			if (!ReferenceEquals(ob.Vehicle, body))
				continue;
			(removed ??= new List<DecisionObligation>()).Add(ob);
		}
		if (removed == null)
			return;

		var newPairs = new HashSet<DecisionObligation>(pairs);
		foreach (var ob in removed)
			newPairs.Remove(ob);
		_ctx.Write(RegisteredCouplings, newPairs);

		var u = _ctx.Read(U);
		if (u != null && u.Count > 0)
		{
			HashSet<DecisionObligation> newU = null;
			foreach (var ob in removed)
			{
				if (!u.Contains(ob))
					continue;
				(newU ??= new HashSet<DecisionObligation>(u)).Remove(ob);
			}
			if (newU != null)
				_ctx.Write(U, newU);
		}

		foreach (var ob in removed)
		{
			if (!_handles.TryGetValue(ob, out var h))
				continue;
			s.RemoveProcedure(h.Maintenance);
			foreach (var obs in h.Observations)
				s.RemoveProcedure(obs);
			_handles.Remove(ob);
		}
	}

	private void InstallProceduresFor(Scheduler s, DecisionObligation ob)
	{
		var maintenance = AddObligationMaintenanceProcedure(s, ob);
		var observations = new List<ProcedureHandle>();
		foreach (var observe in ob.Driver.SimObservations)
		{
			var o = observe;
			observations.Add(s.AddProcedure(() => o(ob.Vehicle)));
		}
		_handles[ob] = (maintenance, observations.ToArray());
	}
}
