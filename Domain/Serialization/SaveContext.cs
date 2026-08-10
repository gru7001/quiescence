using System;
#nullable enable

public sealed class SaveContext
{
	public Registry<Item> Items { get; }
	public Registry<Perk> Perks { get; }
	public Registry<Stat> Stats { get; }
	public Registry<Resource> Resources { get; }
	public SaveSchemaRegistry Schemas { get; }

	public SaveContext(
		Registry<Item> items,
		Registry<Perk> perks,
		Registry<Stat> stats,
		Registry<Resource> resources,
		SaveSchemaRegistry schemas)
	{
		Items = items ?? throw new ArgumentNullException(nameof(items));
		Perks = perks ?? throw new ArgumentNullException(nameof(perks));
		Stats = stats ?? throw new ArgumentNullException(nameof(stats));
		Resources = resources ?? throw new ArgumentNullException(nameof(resources));
		Schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
	}

	public static readonly SaveContext Default = new(
		items: global::Items.Catalog,
		perks: global::PerksCatalog.Catalog,
		stats: global::StatsCatalog.Catalog,
		resources: global::ResourcesCatalog.Catalog,
		schemas: SaveSchemas.Schemas);
}
#nullable disable
