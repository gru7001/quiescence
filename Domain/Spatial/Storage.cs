using System;

/// <summary>
/// A minimal storage/chest occupant: sits on a tile and owns an <see cref="Inventory"/>.
/// Persisted so <see cref="OccupancyPersistence"/> can reference it.
/// </summary>
public sealed class Storage : IOccupant, ISaveable<StoragePersistence.StorageSave>
{
	public Inventory Inventory { get; }

	public Storage(ExecutionContext ctx)
	{
		Inventory = new Inventory(ctx);
	}

	public SaveNode<StoragePersistence.StorageSave> SaveTo(SaveSession session) =>
		new(StoragePersistence.SaveSchemaId, StoragePersistence.Encode(this, session));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}

