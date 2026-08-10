using System;
using System.Collections.Generic;

/// <summary>
/// Identity object for a stat axis (user-facing "stats").
/// The instance itself is the identifier; use <see cref="StatsCatalog"/> for stable ids.
/// </summary>
public sealed class Stat
{
	public readonly string Name;

	public Stat(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

	public override string ToString() => Name;
}

public static class StatsCatalog
{
	public static readonly Stat MoveSpeed = new("MoveSpeed");

	public static readonly Registry<Stat> Catalog = Registry<Stat>.Build(
		("MoveSpeed", MoveSpeed));

	public static IReadOnlyList<Stat> All => Catalog.All;
}

/// <summary>Stat values for a carrier (baseline only for now), backed by <see cref="ExecutionContext"/>.</summary>
public sealed class Stats
{
	private readonly ExecutionContext _ctx;
	private readonly Key<Dictionary<Stat, float>> _values = new();

	public Stats(ExecutionContext ctx) => _ctx = ctx;

	public IReadOnlyDictionary<Stat, float> ReadAll() => _ctx.Read(_values) ?? new Dictionary<Stat, float>();

	public float Read(Stat stat)
	{
		var d = _ctx.Read(_values);
		return d != null && d.TryGetValue(stat, out var v) ? v : 0.0f;
	}

	public void Write(Stat stat, float value)
	{
		var d = _ctx.Read(_values) ?? new Dictionary<Stat, float>();
		var d2 = new Dictionary<Stat, float>(d) { [stat] = value };
		_ctx.Write(_values, d2);
	}
}

