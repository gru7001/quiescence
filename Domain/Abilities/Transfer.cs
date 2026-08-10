using System.Collections.Generic;

public static class Transfer
{
	private static readonly Var TargetVar = Logic.Var("target");
	private static readonly Var ItemVar = Logic.Var("item");
	private static readonly Var AmountVar = Logic.Var("amount");

	/// <summary>Same instance as in deposit/withdraw constraints; used for derived-formula amount-parameter checks.</summary>
	public static readonly Predicate<long> PositiveAmount = Logic.Predicate<long>((_, a) => a > 0);

	private static readonly Predicate<long> FitsInt = Logic.Predicate<long>((_, a) => a <= int.MaxValue);

	/// <summary>
	/// Storage shares a tile with the body or lies across an open edge from an occupied tile.
	/// </summary>
	public static readonly Predicate<Storage> Adjacent = Logic.Predicate<Storage>(IsAdjacentImpl);

	private static bool IsAdjacentImpl(Body body, Storage storage)
	{
		var theirs = new HashSet<Tile>(body.Occupancy.Occupies(storage));
		foreach (var t in body.Occupancy.Occupies(body))
		{
			if (theirs.Contains(t))
				return true;
			for (var d = Direction.Up; d <= Direction.Left; d++)
			{
				var e = t.Edge(d);
				if (e != null && e.Open && e.To != null && theirs.Contains(e.To))
					return true;
			}
		}
		return false;
	}

	/// <summary>Withdraw: item present in the target storage.</summary>
	public static readonly Predicate<Storage, Item> ItemInStorage = Logic.Predicate<Storage, Item>(
		(_, storage, item) => storage.Inventory.Has(item, 1));

	public static readonly CommandDefinition DepositCommand = new Command(
		name: "Deposit",
		variables: [TargetVar, ItemVar, AmountVar],
		static (s, b, a) => IssueDeposit(s, b, a),
		constraint:
			ActionConstraints.BodyIsIdle
			& ParameterPredicates.Occupant[TargetVar]
			& ParameterPredicates.Storage[TargetVar]
			& Adjacent[TargetVar]
			& ParameterPredicates.Item[ItemVar]
			& Consume.ItemInInventoryDomain[ItemVar]
			& ParameterPredicates.Long[AmountVar]
			& PositiveAmount[AmountVar]
			& FitsInt[AmountVar]);

	public static readonly CommandDefinition WithdrawCommand = new Command(
		name: "Withdraw",
		variables: [TargetVar, ItemVar, AmountVar],
		static (s, b, a) => IssueWithdraw(s, b, a),
		constraint:
			ActionConstraints.BodyIsIdle
			& ParameterPredicates.Occupant[TargetVar]
			& ParameterPredicates.Storage[TargetVar]
			& Adjacent[TargetVar]
			& ParameterPredicates.Item[ItemVar]
			& ItemInStorage[TargetVar, ItemVar]
			& ParameterPredicates.Long[AmountVar]
			& PositiveAmount[AmountVar]
			& FitsInt[AmountVar]);

	private static void IssueDeposit(Scheduler scheduler, Body body, Assignment assignment)
	{
		var storage = assignment.Get<Storage>(TargetVar);
		var item = assignment.Get<Item>(ItemVar);
		var amount = (int)assignment.Get<long>(AmountVar);
		body.Inventory.Remove(item, amount);
		storage.Inventory.Add(item, amount);
	}

	private static void IssueWithdraw(Scheduler scheduler, Body body, Assignment assignment)
	{
		var storage = assignment.Get<Storage>(TargetVar);
		var item = assignment.Get<Item>(ItemVar);
		var amount = (int)assignment.Get<long>(AmountVar);
		storage.Inventory.Remove(item, amount);
		body.Inventory.Add(item, amount);
	}
}
