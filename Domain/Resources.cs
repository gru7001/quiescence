using System;
using System.Collections.Generic;

/// <summary>
/// Identity object for a resource (user-facing "resources"; e.g. health, inventory counts).
/// The instance itself is the identifier; use <see cref="ResourcesCatalog"/> for stable ids.
/// </summary>
public sealed class Resource
{
	public readonly string Name;

	public Resource(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

	public override string ToString() => Name;
}

public static class ResourcesCatalog
{
	public static readonly Resource Health = new("Health");

	public static readonly Registry<Resource> Catalog = Registry<Resource>.Build(
		("Health", Health));

	public static IReadOnlyList<Resource> All => Catalog.All;
}

public readonly record struct ResourceValue(float Cur, float Max);

/// <summary>Resource values for a carrier, backed by <see cref="ExecutionContext"/>.</summary>
public sealed class Resources
{
	private readonly ExecutionContext _ctx;
	private readonly Key<Dictionary<Resource, ResourceValue>> _values = new();

	public Resources(ExecutionContext ctx) => _ctx = ctx;

	public IReadOnlyDictionary<Resource, ResourceValue> ReadAll() =>
		_ctx.Read(_values) ?? new Dictionary<Resource, ResourceValue>();

	public ResourceValue Read(Resource res)
	{
		var d = _ctx.Read(_values);
		return d != null && d.TryGetValue(res, out var v) ? v : default;
	}

	public float ReadCur(Resource res) => Read(res).Cur;
	public float ReadMax(Resource res) => Read(res).Max;

	public void WriteMax(Resource res, float max)
	{
		if (max < 0.0f)
			throw new ArgumentOutOfRangeException(nameof(max));
		var cur = ReadCur(res);
		Write(res, new ResourceValue(Cur: Math.Clamp(cur, 0.0f, max), Max: max));
	}

	public void WriteCur(Resource res, float cur)
	{
		var max = ReadMax(res);
		Write(res, new ResourceValue(Cur: Math.Clamp(cur, 0.0f, max), Max: max));
	}

	public void AddCur(Resource res, float delta)
	{
		if (delta == 0.0f)
			return;
		var cur = ReadCur(res);
		WriteCur(res, cur + delta);
	}

	private void Write(Resource res, ResourceValue value)
	{
		var d = _ctx.Read(_values) ?? new Dictionary<Resource, ResourceValue>();
		var d2 = new Dictionary<Resource, ResourceValue>(d) { [res] = value };
		_ctx.Write(_values, d2);
	}
}
