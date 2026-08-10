#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Classical implication for the positive ∧/∨ fragment: <c>F ⊢ G</c> via DNF over
/// <see cref="PredicateCall"/> atoms (predicate identity + argument <see cref="Var"/> references).
/// </summary>
public static class Derivation
{
	private static readonly PredicateCallStructuralComparer CallComparer = new();

	public static bool Derives(Formula premise, Formula conclusion)
	{
		var branches = ToDnf(premise);
		return branches.All(branch => BranchDerives(branch, conclusion));
	}

	private static IReadOnlyList<HashSet<PredicateCall>> ToDnf(Formula formula)
	{
		switch (formula)
		{
			case TrueFormula:
				return new[] { new HashSet<PredicateCall>(CallComparer) };

			case FalseFormula:
				return Array.Empty<HashSet<PredicateCall>>();

			case PredicateCall atom:
				return new[]
				{
					new HashSet<PredicateCall>(CallComparer) { atom },
				};

			case OrFormula or:
				return ToDnf(or.Left).Concat(ToDnf(or.Right)).ToArray();

			case AndFormula and:
				return CrossProduct(ToDnf(and.Left), ToDnf(and.Right));

			default:
				return Array.Empty<HashSet<PredicateCall>>();
		}
	}

	private static IReadOnlyList<HashSet<PredicateCall>> CrossProduct(
		IReadOnlyList<HashSet<PredicateCall>> left,
		IReadOnlyList<HashSet<PredicateCall>> right)
	{
		var result = new List<HashSet<PredicateCall>>();
		foreach (var a in left)
		foreach (var b in right)
		{
			var merged = new HashSet<PredicateCall>(a, CallComparer);
			foreach (var x in b)
				merged.Add(x);
			result.Add(merged);
		}

		return result;
	}

	private static bool BranchDerives(HashSet<PredicateCall> branch, Formula formula)
	{
		switch (formula)
		{
			case TrueFormula:
				return true;

			case FalseFormula:
				return false;

			case PredicateCall atom:
				return branch.Contains(atom);

			case AndFormula and:
				return BranchDerives(branch, and.Left) && BranchDerives(branch, and.Right);

			case OrFormula or:
				return BranchDerives(branch, or.Left) || BranchDerives(branch, or.Right);

			default:
				return false;
		}
	}

	private sealed class PredicateCallStructuralComparer : IEqualityComparer<PredicateCall>
	{
		public bool Equals(PredicateCall? x, PredicateCall? y)
		{
			if (ReferenceEquals(x, y))
				return true;
			if (x == null || y == null)
				return false;
			if (!ReferenceEquals(x.Predicate, y.Predicate))
				return false;
			if (x.Arguments.Count != y.Arguments.Count)
				return false;
			for (var i = 0; i < x.Arguments.Count; i++)
			{
				if (!ReferenceEquals(x.Arguments[i], y.Arguments[i]))
					return false;
			}

			return true;
		}

		public int GetHashCode(PredicateCall obj)
		{
			var h = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Predicate);
			foreach (var a in obj.Arguments)
				h = HashCode.Combine(h, a == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a));
			return h;
		}
	}
}
