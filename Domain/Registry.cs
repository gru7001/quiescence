using System;
using System.Collections.Generic;

/// <summary>
/// Bidirectional registry between stable string ids and identity objects.
/// Intended for catalogs where the instance itself is the runtime identifier,
/// but a stable id string is needed for UI/serialization/debug.
/// </summary>
public sealed class Registry<T> where T : notnull
{
	private readonly Dictionary<string, T> _byId;
	private readonly Dictionary<T, string> _idOf;
	private readonly T[] _all;

	private Registry(Dictionary<string, T> byId, Dictionary<T, string> idOf, T[] all)
	{
		_byId = byId;
		_idOf = idOf;
		_all = all;
	}

	public IReadOnlyDictionary<string, T> ById => _byId;

	public IReadOnlyDictionary<T, string> IdOf => _idOf;

	public IReadOnlyList<T> All => _all;

	public T Get(string id) =>
		_byId.TryGetValue(id, out var thing) ? thing : throw new KeyNotFoundException($"Unknown id '{id}'");

	public string GetId(T thing) =>
		_idOf.TryGetValue(thing, out var id) ? id : throw new KeyNotFoundException("Thing is not registered.");

	public static Registry<T> Build(params (string Id, T Thing)[] pairs)
	{
		if (pairs == null)
			throw new ArgumentNullException(nameof(pairs));

		var byId = new Dictionary<string, T>(pairs.Length, StringComparer.Ordinal);
		var idOf = new Dictionary<T, string>(pairs.Length);
		var all = new T[pairs.Length];

		for (var i = 0; i < pairs.Length; i++)
		{
			var (id, thing) = pairs[i];
			if (id == null)
				throw new ArgumentNullException(nameof(id));
			if (thing == null)
				throw new ArgumentNullException(nameof(thing));

			if (!byId.TryAdd(id, thing))
				throw new InvalidOperationException($"Duplicate id '{id}'.");
			if (!idOf.TryAdd(thing, id))
				throw new InvalidOperationException($"Duplicate thing registered under id '{id}'.");
			all[i] = thing;
		}

		return new Registry<T>(byId, idOf, all);
	}
}

