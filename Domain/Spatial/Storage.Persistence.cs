public static class StoragePersistence
{
	public const string SaveSchemaId = "storage.v1";

	public sealed record StorageSave(InventoryPersistence.InventorySave Inventory);

	public static StorageSave Encode(Storage storage, SaveSession session) =>
		new(Inventory: InventoryPersistence.Encode(storage.Inventory, session));

	public static void Apply(Storage storage, StorageSave save, LoadSession session) =>
		InventoryPersistence.Apply(storage.Inventory, save.Inventory, session);
}

