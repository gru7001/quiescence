using System.Collections.Generic;

public static class Consume
{
	private static readonly Var ItemVar = Logic.Var("item");

	private static readonly Proposition HasAnyConsumable = Logic.Proposition(body =>
	{
		foreach (var it in Items.All)
			if (Items.IsConsumable(it) && body.Inventory.Has(it, 1))
				return true;
		return false;
	});

	/// <summary>Domain for items in inventory; used in <see cref="Command"/> constraint and for derived-formula variable checks.</summary>
	public static readonly DomainPredicate<Item> ItemInInventoryDomain = new(
		generate: PositiveInventoryKeys,
		holds: (body, item) => body.Inventory.Has(item, 1),
		estimateDomain: body => new DomainEstimate(
			ExpectedCount: CountPositiveInventoryKinds(body),
			EnumerationCost: 1,
			ContainsCost: 1,
			Selectivity: null));

	private static IEnumerable<Item> PositiveInventoryKeys(Body body)
	{
		foreach (var kv in body.Inventory.ReadAll())
		{
			if (kv.Value > 0)
				yield return kv.Key;
		}
	}

	private static int CountPositiveInventoryKinds(Body body)
	{
		var n = 0;
		foreach (var kv in body.Inventory.ReadAll())
		{
			if (kv.Value > 0)
				n++;
		}

		return n;
	}

	private static readonly Predicate<Item> IsConsumable = Logic.Predicate<Item>((_, item) =>
		Items.IsConsumable(item));

	public static readonly CommandDefinition Command = new Command(
		name: "Consume",
		variables: [ItemVar],
		static (s, b, a) => Issue(s, b, a),
		constraint:
			ActionConstraints.BodyIsIdle
			& HasAnyConsumable
			& ParameterPredicates.Item[ItemVar]
			& ItemInInventoryDomain[ItemVar]
			& IsConsumable[ItemVar]);

	/// <summary>
	/// Effects only; <see cref="CommandDefinition.TryIssue"/> has already verified <see cref="CommandDefinition.Constraint"/>.
	/// </summary>
	private static void Issue(Scheduler _, Body body, Assignment assignment)
	{
		var item = assignment.Get<Item>(ItemVar);
		body.Inventory.Remove(item, 1);
		Items.ApplyConsumableEffect(body, item);
	}
}
