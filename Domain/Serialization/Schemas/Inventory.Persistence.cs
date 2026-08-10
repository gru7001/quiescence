using System;
using System.Collections.Generic;

public static class InventoryPersistence
{
	public sealed record InventorySave(IReadOnlyDictionary<string, int> CountsByItemId);

	public static InventorySave Encode(Inventory inv, SaveSession session)
	{
		var d = inv.ReadAll();
		var byId = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var (item, n) in d)
			byId[session.Context.Items.GetId(item)] = n;
		return new InventorySave(CountsByItemId: byId);
	}

	public static void Apply(Inventory inv, InventorySave save, LoadSession session)
	{
		foreach (var (itemId, n) in save.CountsByItemId)
			inv.Add(session.Context.Items.Get(itemId), n);
	}
}

