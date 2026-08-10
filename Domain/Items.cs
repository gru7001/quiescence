using System;
using System.Collections.Generic;

/// <summary>
/// Identity object for an item kind.
/// The instance itself is the identifier (prefer using the static catalog in <see cref="Items"/>).
/// </summary>
public sealed class Item
{
	public readonly string Name;

	public Item(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

	public override string ToString() => Name;
}

public enum ConsumableEffectKind
{
	Heal,
}

public readonly record struct ConsumableEffect(ConsumableEffectKind Kind, int Amount);

public sealed record ItemDef(string Name, ConsumableEffect? Consumable);

/// <summary>Static item catalog and definitions (uber minimal; code-authored).</summary>
public static class Items
{
	public static readonly Item Potion = new("Potion");
	public static readonly Item Bread = new("Bread");

	public static readonly Registry<Item> Catalog = Registry<Item>.Build(
		("Potion", Potion),
		("Bread", Bread));

	public static IReadOnlyList<Item> All => Catalog.All;

	private static readonly Dictionary<Item, ItemDef> Defs = new()
	{
		{ Potion, new ItemDef(Name: "Potion", Consumable: new ConsumableEffect(ConsumableEffectKind.Heal, Amount: 5)) },
		{ Bread, new ItemDef(Name: "Bread", Consumable: new ConsumableEffect(ConsumableEffectKind.Heal, Amount: 1)) },
	};

	public static ItemDef Get(Item item) =>
		Defs.TryGetValue(item, out var def)
			? def
			: throw new KeyNotFoundException($"Unknown item '{item.Name}'");

	public static bool IsConsumable(Item item) => Get(item).Consumable != null;

	public static void ApplyConsumableEffect(Body body, Item item)
	{
		var def = Get(item);
		var eff = def.Consumable;
		if (eff == null)
			throw new InvalidOperationException($"Item '{item.Name}' is not consumable.");

		switch (eff.Value.Kind)
		{
			case ConsumableEffectKind.Heal:
				body.Resources.AddCur(ResourcesCatalog.Health, eff.Value.Amount);
				return;
			default:
				throw new InvalidOperationException($"Unhandled consumable effect kind: {eff.Value.Kind}");
		}
	}
}

/// <summary>Inventory state for a body, backed by <see cref="ExecutionContext"/> (counts by item kind).</summary>
public sealed class Inventory
{
	private readonly ExecutionContext _ctx;

	public readonly Key<Dictionary<Item, int>> Counts = new();

	public Inventory(ExecutionContext ctx) => _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

	public IReadOnlyDictionary<Item, int> ReadAll() => _ctx.Read(Counts) ?? new Dictionary<Item, int>();

	public int Count(Item item)
	{
		var d = _ctx.Read(Counts);
		return d != null && d.TryGetValue(item, out var n) ? n : 0;
	}

	public bool Has(Item item, int atLeast = 1) => Count(item) >= atLeast;

	public void Add(Item item, int amount = 1)
	{
		if (amount <= 0)
			throw new ArgumentOutOfRangeException(nameof(amount));

		var d = _ctx.Read(Counts) ?? new Dictionary<Item, int>();
		var d2 = new Dictionary<Item, int>(d);
		d2.TryGetValue(item, out var n);
		d2[item] = n + amount;
		_ctx.Write(Counts, d2);
	}

	public void Remove(Item item, int amount = 1)
	{
		var d = _ctx.Read(Counts);
		var n = d[item];
		var d2 = new Dictionary<Item, int>(d);
		var next = n - amount;
		if (next > 0)
			d2[item] = next;
		else
			d2.Remove(item);
		_ctx.Write(Counts, d2);
	}
}

