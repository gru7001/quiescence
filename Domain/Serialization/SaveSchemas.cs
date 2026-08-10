using System;
using Godot;
#nullable enable

/// <summary>
/// Provides a centralized schema registry for save/load.
/// </summary>
public static class SaveSchemas
{
	public static readonly SaveSchemaRegistry Schemas = BuildSchemas();

	private static SaveSchemaRegistry BuildSchemas()
	{
		var r = new SaveSchemaRegistry();
		RegisterAll(r);
		return r;
	}

	private static void RegisterAll(SaveSchemaRegistry r)
	{
		// Clock
		r.Register<Clock, ClockPersistence.ClockSave>(
			tag: ClockPersistence.SaveSchemaId,
			create: (load, record) => new Clock(load.Ctx),
			apply: ClockPersistence.Apply);

		// Board
		r.Register<World, WorldPersistence.WorldSave>(
			tag: WorldPersistence.SaveSchemaId,
			create: (load, record) => new World(load.Ctx),
			apply: WorldPersistence.Apply);

		r.Register<Tile, TilePersistence.TileSave>(
			tag: TilePersistence.SaveSchemaId,
			create: (load, record) => new Tile(new GroupId(record.Group)),
			apply: TilePersistence.Apply);

		r.Register<Edge, EdgePersistence.EdgeSave>(
			tag: EdgePersistence.SaveSchemaId,
			create: (load, record) => new Edge((Tile)load.Ref(record.From), record.Dir, (Tile)load.Ref(record.To), open: record.Open),
			apply: EdgePersistence.Apply);

		// Occupancy index
		r.Register<Occupancy, OccupancyPersistence.OccupancySave>(
			tag: OccupancyPersistence.SaveSchemaId,
			create: (load, record) => new Occupancy(load.Ctx),
			apply: OccupancyPersistence.Apply);

		// Game
		r.Register<Game, GamePersistence.GameSave>(
			tag: GamePersistence.SaveSchemaId,
			create: (load, record) => new Game(
				(Clock)load.Ref(record.Clock),
				(Occupancy)load.Ref(record.Occupancy),
				(World)load.Ref(record.World),
				new DecisionObligations(load.Ctx)),
			apply: GamePersistence.Apply);

		// Body
		r.Register<Body, BodyPersistence.BodySave>(
			tag: BodyPersistence.SaveSchemaId,
			create: (load, record) => new Body(load.Ctx, (Occupancy)load.Ref(record.Occupancy)),
			apply: BodyPersistence.Apply);

		// Storage
		r.Register<Storage, StoragePersistence.StorageSave>(
			tag: StoragePersistence.SaveSchemaId,
			create: (load, record) => new Storage(load.Ctx),
			apply: StoragePersistence.Apply);

		// Legacy Godot seat driver (no persisted fields; schema id kept for old saves)
		r.Register<LegacyGodotSeatDriver, LegacyGodotSeatDriverPersistence.DriverSave>(
			tag: LegacyGodotSeatDriverPersistence.SaveSchemaId,
			create: (load, record) => new LegacyGodotSeatDriver(load.SeatRoot),
			apply: LegacyGodotSeatDriverPersistence.Apply);

		// Presentation Godot seat driver
		r.Register<GodotSeatDriver, GodotSeatDriverPersistence.DriverSave>(
			tag: GodotSeatDriverPersistence.SaveSchemaId,
			create: (load, record) => new GodotSeatDriver(load.SeatRoot),
			apply: GodotSeatDriverPersistence.Apply);

		r.Register<FooDriver, FooDriverPersistence.DriverSave>(
			tag: FooDriverPersistence.SaveSchemaId,
			create: (load, record) => new FooDriver(record.WaitDeltaTicks),
			apply: FooDriverPersistence.Apply);

		// Decision obligations (registered couplings only; <see cref="DecisionObligations.U"/> is runtime-derived)
		r.Register<DecisionObligations, DecisionObligationsPersistence.ObligationsSave>(
			tag: DecisionObligationsPersistence.SaveSchemaId,
			create: (load, record) => new DecisionObligations(load.Ctx),
			apply: DecisionObligationsPersistence.Apply);
	}
}
#nullable disable
