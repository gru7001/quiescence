using System;
using System.Collections.Generic;

public static class PerksPersistence
{
	public sealed record PerksSave(IReadOnlyList<string> OwnedPerkIds);

	public static PerksSave Encode(Perks perks, SaveSession session)
	{
		if (perks == null) throw new ArgumentNullException(nameof(perks));
		if (session == null) throw new ArgumentNullException(nameof(session));

		var owned = perks.ReadOwned();
		var perkIds = new List<string>();
		foreach (var perk in owned)
			perkIds.Add(session.Context.Perks.GetId(perk));
		return new PerksSave(OwnedPerkIds: perkIds);
	}

	public static void Apply(Perks perks, PerksSave save, LoadSession session)
	{
		if (perks == null) throw new ArgumentNullException(nameof(perks));
		if (save == null) throw new ArgumentNullException(nameof(save));
		if (session == null) throw new ArgumentNullException(nameof(session));

		foreach (var perkId in save.OwnedPerkIds)
			perks.Add(session.Context.Perks.Get(perkId));
	}
}

