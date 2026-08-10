public static class BodyPersistence
{
	public const string SaveSchemaId = "body.v4";

	public sealed record BodySave(
		NodeRef Occupancy,
		StatsPersistence.StatsSave Stats,
		ResourcesPersistence.ResourcesSave Resources,
		InventoryPersistence.InventorySave Inventory,
		PerksPersistence.PerksSave Perks,
		BestTryActionStateCodec.ActionStateSave ActionState);

	public static BodySave Encode(Body body, SaveSession session) =>
		new(
			Occupancy: session.Ref(body.Occupancy),
			Stats: StatsPersistence.Encode(body.Stats, session),
			Resources: ResourcesPersistence.Encode(body.Resources, session),
			Inventory: InventoryPersistence.Encode(body.Inventory, session),
			Perks: PerksPersistence.Encode(body.Perks, session),
			ActionState: BestTryActionStateCodec.Encode(body.ReadActionState(), session));

	public static void Apply(Body body, BodySave save, LoadSession session)
	{
		StatsPersistence.Apply(body.Stats, save.Stats, session);
		ResourcesPersistence.Apply(body.Resources, save.Resources, session);
		InventoryPersistence.Apply(body.Inventory, save.Inventory, session);
		PerksPersistence.Apply(body.Perks, save.Perks, session);

		var st = BestTryActionStateCodec.Decode(save.ActionState, session) as IActionState;
		body.WriteActionState(st);
	}
}
