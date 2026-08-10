#nullable enable
using System;
using System.Collections.Generic;

/// <summary>Opaque predicate: classical <see cref="Holds"/> over resolved argument values.</summary>
public interface IPredicate
{
	int Arity { get; }

	IReadOnlyList<Type> ArgumentTypes { get; }

	bool Holds(Body body, IReadOnlyList<object?> args);

	PredicateEstimate Estimate(Body body);
}

/// <summary>Domain predicate: exposes a constructible satisfying relation via <see cref="Generate"/>.</summary>
public interface IDomainPredicate : IPredicate
{
	IEnumerable<IReadOnlyList<object?>> Generate(Body body);

	DomainEstimate EstimateDomain(Body body);
}

public sealed record PredicateEstimate(double? VerificationCost);

public sealed record DomainEstimate(
	double? ExpectedCount,
	double? EnumerationCost,
	double? ContainsCost,
	double? Selectivity);

/// <summary>Arity-0 predicate (proposition).</summary>
public sealed class Proposition : IPredicate
{
	private readonly Func<Body, bool> _holds;
	private readonly PredicateEstimate _estimate;

	public Proposition(Func<Body, bool> holds, PredicateEstimate? estimate = null)
	{
		_holds = holds;
		_estimate = estimate ?? new PredicateEstimate(VerificationCost: 1);
	}

	public int Arity => 0;

	public IReadOnlyList<Type> ArgumentTypes => Array.Empty<Type>();

	public static implicit operator Formula(Proposition p) =>
		new PredicateCall(p, Array.Empty<Var>());

	public static Formula operator &(Proposition left, Proposition right) =>
		(Formula)left & (Formula)right;

	public static Formula operator &(Proposition left, Formula right) =>
		(Formula)left & right;

	public bool Holds(Body body, IReadOnlyList<object?> args) => _holds(body);

	public PredicateEstimate Estimate(Body body) => _estimate;
}

/// <summary>Unary opaque predicate.</summary>
public sealed class Predicate<T> : IPredicate
{
	private readonly Func<Body, T, bool> _holds;
	private readonly PredicateEstimate _estimate;

	public Predicate(Func<Body, T, bool> holds, PredicateEstimate? estimate = null)
	{
		_holds = holds;
		_estimate = estimate ?? new PredicateEstimate(VerificationCost: 1);
	}

	public int Arity => 1;

	public IReadOnlyList<Type> ArgumentTypes => new[] { typeof(T) };

	public Formula this[Var arg] => new PredicateCall(this, new[] { arg });

	public bool Holds(Body body, IReadOnlyList<object?> args) =>
		_holds(body, (T)args[0]!);

	public PredicateEstimate Estimate(Body body) => _estimate;
}

/// <summary>Binary opaque predicate.</summary>
public sealed class Predicate<T1, T2> : IPredicate
{
	private readonly Func<Body, T1, T2, bool> _holds;
	private readonly PredicateEstimate _estimate;

	public Predicate(Func<Body, T1, T2, bool> holds, PredicateEstimate? estimate = null)
	{
		_holds = holds;
		_estimate = estimate ?? new PredicateEstimate(VerificationCost: 1);
	}

	public int Arity => 2;

	public IReadOnlyList<Type> ArgumentTypes => new[] { typeof(T1), typeof(T2) };

	public Formula this[Var a, Var b] => new PredicateCall(this, new[] { a, b });

	public bool Holds(Body body, IReadOnlyList<object?> args) =>
		_holds(body, (T1)args[0]!, (T2)args[1]!);

	public PredicateEstimate Estimate(Body body) => _estimate;
}

/// <summary>Unary domain predicate (enumerable extension).</summary>
public sealed class DomainPredicate<T> : IDomainPredicate
{
	private readonly Func<Body, IEnumerable<T>> _generate;
	private readonly Func<Body, T, bool> _holds;
	private readonly Func<Body, DomainEstimate> _estimateDomain;

	public DomainPredicate(
		Func<Body, IEnumerable<T>> generate,
		Func<Body, T, bool> holds,
		Func<Body, DomainEstimate> estimateDomain)
	{
		_generate = generate;
		_holds = holds;
		_estimateDomain = estimateDomain;
	}

	public int Arity => 1;

	public IReadOnlyList<Type> ArgumentTypes => new[] { typeof(T) };

	public Formula this[Var arg] => new PredicateCall(this, [ arg ]);

	public IEnumerable<IReadOnlyList<object?>> Generate(Body body)
	{
		foreach (var x in _generate(body))
			yield return new object?[] { x! };
	}

	public bool Holds(Body body, IReadOnlyList<object?> args) =>
		_holds(body, (T)args[0]!);

	public PredicateEstimate Estimate(Body body)
	{
		var d = _estimateDomain(body);
		return new PredicateEstimate(VerificationCost: d.ContainsCost);
	}

	public DomainEstimate EstimateDomain(Body body) => _estimateDomain(body);
}

/// <summary>Binary domain predicate.</summary>
public sealed class DomainPredicate<T1, T2> : IDomainPredicate
{
	private readonly Func<Body, IEnumerable<(T1, T2)>> _generate;
	private readonly Func<Body, T1, T2, bool> _holds;
	private readonly Func<Body, DomainEstimate> _estimateDomain;

	public DomainPredicate(
		Func<Body, IEnumerable<(T1, T2)>> generate,
		Func<Body, T1, T2, bool> holds,
		Func<Body, DomainEstimate> estimateDomain)
	{
		_generate = generate;
		_holds = holds;
		_estimateDomain = estimateDomain;
	}

	public int Arity => 2;

	public IReadOnlyList<Type> ArgumentTypes => new[] { typeof(T1), typeof(T2) };

	public Formula this[Var a, Var b] => new PredicateCall(this, new[] { a, b });

	public IEnumerable<IReadOnlyList<object?>> Generate(Body body)
	{
		foreach (var (a, b) in _generate(body))
			yield return new object?[] { a!, b! };
	}

	public bool Holds(Body body, IReadOnlyList<object?> args) =>
		_holds(body, (T1)args[0]!, (T2)args[1]!);

	public PredicateEstimate Estimate(Body body)
	{
		var d = _estimateDomain(body);
		return new PredicateEstimate(VerificationCost: d.ContainsCost);
	}

	public DomainEstimate EstimateDomain(Body body) => _estimateDomain(body);
}

public static class Logic
{
	public static Var Var(string name) => new Var(name);

	public static Proposition Proposition(Func<Body, bool> holds, PredicateEstimate? estimate = null) =>
		new(holds, estimate);

	public static Predicate<T> Predicate<T>(Func<Body, T, bool> holds, PredicateEstimate? estimate = null) =>
		new(holds, estimate);

	public static Predicate<T1, T2> Predicate<T1, T2>(
		Func<Body, T1, T2, bool> holds,
		PredicateEstimate? estimate = null) =>
		new(holds, estimate);

	public static DomainPredicate<T> DomainPredicate<T>(
		Func<Body, IEnumerable<T>> generate,
		Func<Body, T, bool> holds,
		Func<Body, DomainEstimate> estimateDomain) =>
		new(generate, holds, estimateDomain);

	public static DomainPredicate<T1, T2> DomainPredicate<T1, T2>(
		Func<Body, IEnumerable<(T1, T2)>> generate,
		Func<Body, T1, T2, bool> holds,
		Func<Body, DomainEstimate> estimateDomain) =>
		new(generate, holds, estimateDomain);
}
