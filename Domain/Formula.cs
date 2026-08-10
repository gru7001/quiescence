#nullable enable
using System;
using System.Collections.Generic;

/// <summary>Operational truth under a possibly partial <see cref="Assignment"/>.</summary>
public enum PartialTruth
{
	False = 0,
	Unknown = 1,
	True = 2,
}

// --- Vars & assignment (guide §2–3): untyped Var, immutable binding -------------------------------

/// <summary>Untyped logic variable; CLR sorts come from predicates on this symbol.</summary>
public sealed class Var
{
	public string Name { get; }

	public Var(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

	public bool TryGetBoundValue(Assignment assignment, out object? value) =>
		assignment.TryGet(this, out value);

	public Assignment? BindOrCheck(Assignment assignment, object? value)
	{
		if (assignment.TryGet(this, out var existing))
			return Equals(existing, value) ? assignment : null;
		return assignment.Bind(this, value);
	}

	public override string ToString() => Name;
}

/// <summary>Immutable assignment: binds <see cref="Var"/> to runtime objects (guide §3).</summary>
public sealed class Assignment
{
	private readonly Dictionary<Var, object?> _values;

	public Assignment() => _values = new Dictionary<Var, object?>();

	private Assignment(Dictionary<Var, object?> values) => _values = values;

	public bool TryGet(Var variable, out object? value)
	{
		if (_values.TryGetValue(variable, out var raw))
		{
			value = raw;
			return true;
		}

		value = null;
		return false;
	}

	public bool TryGet<T>(Var variable, out T value)
	{
		if (TryGet(variable, out var raw) && raw is T t)
		{
			value = t;
			return true;
		}

		value = default!;
		return false;
	}

	public T Get<T>(Var variable)
	{
		if (TryGet(variable, out T value))
			return value;
		throw new InvalidOperationException(
			$"Assignment missing bound value for '{variable.Name}' as {typeof(T).Name}.");
	}

	public Assignment Bind(Var variable, object? value)
	{
		var next = new Dictionary<Var, object?>(_values.Count + 1);
		foreach (var pair in _values)
			next[pair.Key] = pair.Value;
		next[variable] = value;
		return new Assignment(next);
	}
}

// --- Formula AST (guide §4): True, False, ∧, ∨, predicate application -----------------------------

/// <summary>Positive propositional fragment over <see cref="PredicateCall"/> atoms.</summary>
public abstract class Formula
{
	public static readonly Formula True = new TrueFormula();

	public static readonly Formula False = new FalseFormula();

	public static Formula operator &(Formula a, Formula b) => new AndFormula(a, b);

	public static Formula operator |(Formula a, Formula b) => new OrFormula(a, b);

	/// <summary>Structural classical implication in the positive ∧/∨ fragment; see <see cref="Derivation"/>.</summary>
	public bool Derives(Formula conclusion) => Derivation.Derives(this, conclusion);

	public abstract void CollectAtoms(List<object> into);

	/// <summary>Three-valued evaluation under a possibly partial <paramref name="partialAssignment"/>.</summary>
	public abstract PartialTruth Evaluate(Body body, Assignment partialAssignment);

	/// <summary>True when this formula evaluates to <see cref="PartialTruth.True"/> under <paramref name="assignment"/>.</summary>
	public bool Accepts(Body body, Assignment assignment) =>
		Evaluate(body, assignment) == PartialTruth.True;

	/// <summary>True when this formula is not already refuted by <paramref name="partialAssignment"/>.</summary>
	public bool Extendable(Body body, Assignment partialAssignment) =>
		Evaluate(body, partialAssignment) != PartialTruth.False;

	/// <summary>Replace every occurrence of <paramref name="from"/> with <paramref name="to"/> in this formula.</summary>
	public abstract Formula Substitute(Var from, Var to);

	public static Formula operator &(Formula left, Proposition right) =>
		left & (Formula)right;
}

public sealed class TrueFormula : Formula
{
	public override void CollectAtoms(List<object> into) { }

	public override PartialTruth Evaluate(Body body, Assignment assignment) =>
		PartialTruth.True;

	public override Formula Substitute(Var from, Var to) => this;
}

public sealed class FalseFormula : Formula
{
	public override void CollectAtoms(List<object> into) { }

	public override PartialTruth Evaluate(Body body, Assignment assignment) =>
		PartialTruth.False;

	public override Formula Substitute(Var from, Var to) => this;
}

public sealed class AndFormula : Formula
{
	public Formula Left { get; }
	public Formula Right { get; }

	public AndFormula(Formula left, Formula right)
	{
		Left = left;
		Right = right;
	}

	public override void CollectAtoms(List<object> into)
	{
		Left.CollectAtoms(into);
		Right.CollectAtoms(into);
	}

	public override PartialTruth Evaluate(Body body, Assignment assignment)
	{
		var a = Left.Evaluate(body, assignment);
		if (a == PartialTruth.False)
			return PartialTruth.False;
		var b = Right.Evaluate(body, assignment);
		if (b == PartialTruth.False)
			return PartialTruth.False;
		if (a == PartialTruth.Unknown || b == PartialTruth.Unknown)
			return PartialTruth.Unknown;
		return PartialTruth.True;
	}

	public override Formula Substitute(Var from, Var to) =>
		Left.Substitute(from, to) & Right.Substitute(from, to);
}

public sealed class OrFormula : Formula
{
	public Formula Left { get; }
	public Formula Right { get; }

	public OrFormula(Formula left, Formula right)
	{
		Left = left;
		Right = right;
	}

	public override void CollectAtoms(List<object> into)
	{
		Left.CollectAtoms(into);
		Right.CollectAtoms(into);
	}

	public override PartialTruth Evaluate(Body body, Assignment assignment)
	{
		var a = Left.Evaluate(body, assignment);
		if (a == PartialTruth.True)
			return PartialTruth.True;
		var b = Right.Evaluate(body, assignment);
		if (b == PartialTruth.True)
			return PartialTruth.True;
		if (a == PartialTruth.Unknown || b == PartialTruth.Unknown)
			return PartialTruth.Unknown;
		return PartialTruth.False;
	}

	public override Formula Substitute(Var from, Var to) =>
		Left.Substitute(from, to) | Right.Substitute(from, to);
}

/// <summary>Predicate application: <see cref="IPredicate"/> with argument <see cref="Var"/>s (guide §4).</summary>
public sealed class PredicateCall : Formula
{
	public IPredicate Predicate { get; }
	public IReadOnlyList<Var> Arguments { get; }

	public PredicateCall(IPredicate predicate, IReadOnlyList<Var> arguments)
	{
		Predicate = predicate;
		Arguments = arguments;
	}

	public override void CollectAtoms(List<object> into) => into.Add(Predicate);

	public override PartialTruth Evaluate(Body body, Assignment assignment)
	{
		var args = new object?[Arguments.Count];
		for (var i = 0; i < Arguments.Count; i++)
		{
			if (!Arguments[i].TryGetBoundValue(assignment, out var v))
				return PartialTruth.Unknown;
			args[i] = v;
		}

		return Predicate.Holds(body, args) ? PartialTruth.True : PartialTruth.False;
	}

	public override Formula Substitute(Var from, Var to)
	{
		Var[]? next = null;
		for (var i = 0; i < Arguments.Count; i++)
		{
			if (!ReferenceEquals(Arguments[i], from))
				continue;
			next ??= CopyArgs();
			next[i] = to;
		}
		return next == null ? this : new PredicateCall(Predicate, next);
	}

	Var[] CopyArgs()
	{
		var a = new Var[Arguments.Count];
		for (var i = 0; i < Arguments.Count; i++)
			a[i] = Arguments[i];
		return a;
	}
}
