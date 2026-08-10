using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class SaveSchemaRegistry
{
	private readonly Dictionary<string, Type> _tagToType = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Type> _tagToRecordType = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Func<LoadSession, object, object>> _create = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Action<object, object, LoadSession>> _apply = new(StringComparer.Ordinal);

	public void Register<TModel, TRecord>(
		string tag,
		Func<LoadSession, TRecord, TModel> create,
		Action<TModel, TRecord, LoadSession> apply)
	{
		if (tag == null) throw new ArgumentNullException(nameof(tag));
		if (create == null) throw new ArgumentNullException(nameof(create));
		if (apply == null) throw new ArgumentNullException(nameof(apply));

		var t = typeof(TModel);
		var tr = typeof(TRecord);

		if (!_tagToType.TryAdd(tag, t))
			throw new InvalidOperationException($"Tag '{tag}' is already registered.");
		if (!_tagToRecordType.TryAdd(tag, tr))
			throw new InvalidOperationException($"Record type for tag '{tag}' is already registered.");
		if (!_create.TryAdd(tag, (load, record) => create(load, (TRecord)record)!))
			throw new InvalidOperationException($"Create for tag '{tag}' is already registered.");
		if (!_apply.TryAdd(tag, (target, record, load) => apply((TModel)target, (TRecord)record, load)))
			throw new InvalidOperationException($"Apply for tag '{tag}' is already registered.");
	}

	public Type TypeFor(string tag) =>
		_tagToType.TryGetValue(tag, out var t)
			? t
			: throw new KeyNotFoundException($"Unknown save tag '{tag}'.");

	public object DeserializeRecord(string tag, JsonElement record)
	{
		if (tag == null) throw new ArgumentNullException(nameof(tag));

		if (!_tagToRecordType.TryGetValue(tag, out var recordType))
			throw new InvalidOperationException($"No record type registered for tag '{tag}'.");

		return record.Deserialize(recordType, SaveJson.Options)
		       ?? throw new InvalidOperationException($"Failed to deserialize record for tag '{tag}'.");
	}

	public object Create(string tag, object record, LoadSession session)
	{
		if (tag == null) throw new ArgumentNullException(nameof(tag));
		if (session == null) throw new ArgumentNullException(nameof(session));

		if (!_create.TryGetValue(tag, out var create))
			throw new InvalidOperationException($"No create registered for tag '{tag}'.");

		var obj = create(session, record);
		var expected = TypeFor(tag);
		if (!expected.IsInstanceOfType(obj))
			throw new InvalidOperationException($"Create result type mismatch for '{tag}'. Expected {expected.Name}, got {obj.GetType().Name}.");
		return obj;
	}

	public object Create(string tag, JsonElement record, LoadSession session) =>
		Create(tag, DeserializeRecord(tag, record), session);

	public void Apply(string tag, object target, object record, LoadSession session)
	{
		if (tag == null) throw new ArgumentNullException(nameof(tag));
		if (target == null) throw new ArgumentNullException(nameof(target));
		if (session == null) throw new ArgumentNullException(nameof(session));

		if (!_apply.TryGetValue(tag, out var apply))
			throw new InvalidOperationException($"No apply registered for tag '{tag}'.");

		var expected = TypeFor(tag);
		if (!expected.IsInstanceOfType(target))
			throw new InvalidOperationException($"Apply target type mismatch for '{tag}'. Expected {expected.Name}, got {target.GetType().Name}.");

		apply(target, record, session);
	}

	public void Apply(string tag, object target, JsonElement record, LoadSession session) =>
		Apply(tag, target, DeserializeRecord(tag, record), session);
}
