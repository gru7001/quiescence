#nullable enable
using System;
using System.Collections.Generic;

/// <summary>
/// Parameter sort markers for <see cref="Derivation.Derives"/> / typing.
/// <see cref="Direction"/> is also a closed <see cref="IDomainPredicate"/> (four values); <see cref="Item"/>, <see cref="Tile"/>, <see cref="Long"/> are not enumerable domains here.
/// </summary>
public static class ParameterPredicates
{
	public static readonly DomainPredicate<Direction> Direction = Logic.DomainPredicate<Direction>(
		GenerateDirections,
		(_, d) => Enum.IsDefined(typeof(global::Direction), d),
		_ => new DomainEstimate(ExpectedCount: 4, EnumerationCost: 4, ContainsCost: 1, Selectivity: null));

	public static readonly Predicate<Item> Item =
		Logic.Predicate<Item>((_, item) => item != null);

	public static readonly Predicate<Tile> Tile =
		Logic.Predicate<Tile>((_, t) => t != null);

	/// <summary>Tick deltas, amounts, etc.</summary>
	public static readonly Predicate<long> Long =
		Logic.Predicate<long>((_, _) => true);

	public static readonly Predicate<IOccupant> Occupant =
		Logic.Predicate<IOccupant>((_, o) => o != null);

	public static readonly Predicate<Storage> Storage =
		Logic.Predicate<Storage>((_, s) => s != null);

	static IEnumerable<Direction> GenerateDirections(Body _) =>
		new[] { global::Direction.Up, global::Direction.Right, global::Direction.Down, global::Direction.Left };
}
