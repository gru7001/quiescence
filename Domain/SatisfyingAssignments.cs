#nullable enable
using System;
using System.Collections.Generic;

/// <summary>Pick derived <c>G</c> with <c>F ⊢ G</c>, lowest finite domain volume, enumerate <c>G</c> via <see cref="IDomainPredicate"/>, filter with <c>F</c>.</summary>
public static class SatisfyingAssignments
{
	public static IEnumerable<Assignment> Enumerate(Formula formula, Body body)
	{
		if (formula == null || body == null)
			yield break;

		var required = new HashSet<Var>();
		CollectVars(formula, required);

		Formula? best = null;
		var bestVol = double.PositiveInfinity;

		var seen = new HashSet<Formula>();
		foreach (var g in DerivedNodes(formula, seen))
		{
			if (!Covers(required, g))
				continue;
			if (!Derivation.Derives(formula, g))
				continue;

			var vol = ExpectedVolume(body, g);
			if (vol < bestVol || (vol == bestVol && AtomCount(g) < AtomCount(best)))
			{
				bestVol = vol;
				best = g;
			}
		}

		if (best == null || double.IsPositiveInfinity(bestVol))
			yield break;

		foreach (var a in GenerateConjunction(body, best))
		{
			if (formula.Accepts(body, a))
				yield return a;
		}
	}

	static int AtomCount(Formula? f)
	{
		if (f == null)
			return int.MaxValue;
		var calls = new List<PredicateCall>();
		return TryConjunctAtoms(f, calls) ? calls.Count : int.MaxValue;
	}

	static void CollectVars(Formula f, HashSet<Var> into)
	{
		switch (f)
		{
			case PredicateCall pc:
				foreach (var a in pc.Arguments)
					into.Add(a);
				return;
			case AndFormula and:
				CollectVars(and.Left, into);
				CollectVars(and.Right, into);
				return;
			case OrFormula or:
				CollectVars(or.Left, into);
				CollectVars(or.Right, into);
				return;
			default:
				return;
		}
	}

	static IEnumerable<Formula> DerivedNodes(Formula f, HashSet<Formula> seen)
	{
		if (!seen.Add(f))
			yield break;

		yield return f;
		switch (f)
		{
			case AndFormula and:
				foreach (var x in DerivedNodes(and.Left, seen))
					yield return x;
				foreach (var x in DerivedNodes(and.Right, seen))
					yield return x;
				break;
			case OrFormula or:
				foreach (var x in DerivedNodes(or.Left, seen))
					yield return x;
				foreach (var x in DerivedNodes(or.Right, seen))
					yield return x;
				break;
		}
	}

	static bool Covers(HashSet<Var> required, Formula g)
	{
		var have = new HashSet<Var>();
		CollectVars(g, have);
		foreach (var v in required)
		{
			if (!have.Contains(v))
				return false;
		}

		return true;
	}

	/// <summary>Only a flat <c>∧</c> of atoms has finite product volume; anything else (e.g. <c>∨</c>) or a non-<see cref="IDomainPredicate"/> atom is infinite.</summary>
	static bool TryConjunctAtoms(Formula g, List<PredicateCall> calls)
	{
		switch (g)
		{
			case TrueFormula:
				return true;
			case FalseFormula:
				return false;
			case PredicateCall pc:
				calls.Add(pc);
				return true;
			case AndFormula and:
				return TryConjunctAtoms(and.Left, calls) && TryConjunctAtoms(and.Right, calls);
			default:
				return false;
		}
	}

	static double ExpectedVolume(Body body, Formula g)
	{
		if (g is TrueFormula)
			return 1;

		var calls = new List<PredicateCall>();
		if (!TryConjunctAtoms(g, calls))
			return double.PositiveInfinity;

		var boundVar = new HashSet<Var>();
		var prod = 1.0;
		foreach (var pc in calls)
		{
			if (pc.Predicate is not IDomainPredicate d)
				return double.PositiveInfinity;

			foreach (var v in pc.Arguments)
			{
				if (!boundVar.Add(v))
					return double.PositiveInfinity;
			}

			var ec = d.EstimateDomain(body).ExpectedCount;
			if (ec is not { } c || c <= 0 || double.IsPositiveInfinity(c) || double.IsNaN(c))
				return double.PositiveInfinity;
			prod *= c;
		}

		return prod;
	}

	static IEnumerable<Assignment> GenerateConjunction(Body body, Formula g)
	{
		if (g is TrueFormula)
		{
			yield return new Assignment();
			yield break;
		}

		var calls = new List<PredicateCall>();
		if (!TryConjunctAtoms(g, calls))
			yield break;

		foreach (var pc in calls)
		{
			if (pc.Predicate is not IDomainPredicate)
				yield break;
		}

		foreach (var a in CartesianAtoms(body, calls, 0, new Assignment()))
			yield return a;
	}

	static IEnumerable<Assignment> CartesianAtoms(Body body, List<PredicateCall> calls, int i, Assignment cur)
	{
		if (i == calls.Count)
		{
			yield return cur;
			yield break;
		}

		var pc = calls[i];
		var d = (IDomainPredicate)pc.Predicate;
		foreach (var tuple in d.Generate(body))
		{
			Assignment? next = cur;
			for (var j = 0; j < pc.Arguments.Count && next != null; j++)
				next = pc.Arguments[j].BindOrCheck(next, tuple[j]);

			if (next == null)
				continue;
			foreach (var r in CartesianAtoms(body, calls, i + 1, next))
				yield return r;
		}
	}
}
