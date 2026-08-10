using System.Collections.Generic;

/// <summary>
/// Actor-scoped aggregate that can have presence on multiple boards simultaneously (e.g. while crossing connectors).
/// Owns the per-body action-state key driven by <see cref="ActionDispatch"/>.
/// </summary>
public sealed class Body : IOccupant, ISaveable<BodyPersistence.BodySave>
{
	public ExecutionContext Ctx { get; }
	public Occupancy Occupancy { get; }
	public Perks Perks { get; }
	public Inventory Inventory { get; }
	public Stats Stats { get; }
	public Resources Resources { get; }

	/// <summary>
	/// Current action stage. Writes are dispatched via <see cref="ActionDispatch"/>; <see cref="IActionState.OnEnter"/>
	/// runs on each change.
	/// </summary>
	public readonly Key<IActionState> ActionState = new();

	public Body(ExecutionContext ctx, Occupancy occupancy)
	{
		Ctx = ctx;
		Occupancy = occupancy;
		Perks = new Perks(ctx);
		Inventory = new Inventory(ctx);
		Stats = new Stats(ctx);
		Resources = new Resources(ctx);
	}

	public IActionState ReadActionState() => Ctx.Read(ActionState);

	public void WriteActionState(IActionState state) => Ctx.Write(ActionState, state);

	public bool IsDead() => Resources.ReadCur(ResourcesCatalog.Health) <= 0.0f;

	/// <summary>
	/// Body-level corpsification: remove this body from the world (occupancy).
	/// Caller (typically <see cref="Game.OnBodyDeath"/>) is responsible for the surrounding
	/// runtime teardown (action dispatch, decision-system couplings, removal from <see cref="Game.Bodies"/>).
	/// Idempotent.
	/// </summary>
	public void Corpsify()
	{
		var tiles = OccupiedTiles();
		for (var i = 0; i < tiles.Length; i++)
			Occupancy.Remove(this, tiles[i]);
	}

	public Tile[] OccupiedTiles()
	{
		var set = new HashSet<Tile>();
		foreach (var t in Occupancy.Occupies(this))
			set.Add(t);
		var a = new Tile[set.Count];
		set.CopyTo(a);
		return a;
	}

	public SaveNode<BodyPersistence.BodySave> SaveTo(SaveSession session) =>
		new(BodyPersistence.SaveSchemaId, BodyPersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}
