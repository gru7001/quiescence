using System;
using System.Collections.Generic;

/// <summary>
/// Identity object for a perk.
/// The instance itself is the identifier (prefer using the static catalog in <see cref="PerksCatalog"/>).
/// </summary>
public sealed class Perk
{
	public readonly string Name;

	public Perk(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

	public override string ToString() => Name;
}

public static class PerksCatalog
{
	public static readonly Perk Pyromancer = new("Pyromancer");

	public static readonly Registry<Perk> Catalog = Registry<Perk>.Build(
		("Pyromancer", Pyromancer));

	public static IReadOnlyList<Perk> All => Catalog.All;
}

/// <summary>Perk ownership state for a body, backed by <see cref="ExecutionContext"/>.</summary>
public sealed class Perks
{
	private readonly ExecutionContext _ctx;

	public readonly Key<HashSet<Perk>> Owned = new();

	public Perks(ExecutionContext ctx) => _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

	public IReadOnlyCollection<Perk> ReadOwned() => _ctx.Read(Owned) ?? (IReadOnlyCollection<Perk>)Array.Empty<Perk>();

	public bool Has(Perk perk)
	{
		var set = _ctx.Read(Owned);
		return set != null && set.Contains(perk);
	}

	public void Add(Perk perk)
	{
		var set = _ctx.Read(Owned) ?? new HashSet<Perk>();
		if (set.Contains(perk))
			return;
		var s2 = new HashSet<Perk>(set) { perk };
		_ctx.Write(Owned, s2);
	}

	public void Remove(Perk perk)
	{
		var set = _ctx.Read(Owned);
		if (set == null || !set.Contains(perk))
			return;
		var s2 = new HashSet<Perk>(set);
		s2.Remove(perk);
		_ctx.Write(Owned, s2);
	}
}

