using System;
using System.Collections.Generic;
using Godot;

public sealed class LoadSession
{
	private readonly Dictionary<string, SaveRecord> _records = new(StringComparer.Ordinal);
	internal IEnumerable<SaveRecord> Records => _records.Values;

	private readonly Dictionary<string, object> _objs = new(StringComparer.Ordinal);
	private readonly Dictionary<string, object> _typedRecords = new(StringComparer.Ordinal);
	private readonly Queue<Action> _work = new();

	private readonly State _state;
	internal readonly ExecutionContext Ctx;

	public readonly SaveContext Context;
	public SaveSchemaRegistry Schemas => Context.Schemas;

	/// <summary>
	/// Godot-side runtime anchor for creating UI/input-backed drivers during load.
	/// </summary>
	public readonly Node SeatRoot;

	public LoadSession(SaveContext context, Node seatRoot)
	{
		Context = context ?? SaveContext.Default;
		SeatRoot = seatRoot ?? throw new ArgumentNullException(nameof(seatRoot));
		_state = new State();
		Ctx = new ExecutionContext(_state);
	}

	public void Index(SaveFile save)
	{
		if (save == null) throw new ArgumentNullException(nameof(save));
		_records.Clear();
		_objs.Clear();
		_typedRecords.Clear();
		foreach (var n in save.Nodes)
		{
			if (!_records.TryAdd(n.Id, n))
				throw new InvalidOperationException($"Duplicate save node id '{n.Id}'.");
		}
	}

	public object Ref(NodeRef r)
	{
		if (NodeRefs.IsNull(r))
			return null;

		if (_objs.TryGetValue(r.Id, out var existing))
			return existing;
		if (!_records.TryGetValue(r.Id, out var node))
			throw new KeyNotFoundException($"Unknown node id '{r.Id}'.");

		if (!_typedRecords.TryGetValue(r.Id, out var typedRecord))
		{
			typedRecord = Schemas.DeserializeRecord(node.Tag, node.Record);
			_typedRecords.Add(r.Id, typedRecord);
		}

		var obj = Schemas.Create(node.Tag, typedRecord, this);
		_objs.Add(r.Id, obj);
		_work.Enqueue(() => Schemas.Apply(node.Tag, obj, typedRecord, this));
		return obj;
	}

	public object ResolveRef(RefSave r) => r.Kind switch
	{
		"Item" => Context.Items.Get(r.Id),
		"Perk" => Context.Perks.Get(r.Id),
		"Stat" => Context.Stats.Get(r.Id),
		"Resource" => Context.Resources.Get(r.Id),
		_ => throw new InvalidOperationException($"Unknown ref kind '{r.Kind}'.")
	};

	public void Drain()
	{
		while (_work.Count > 0)
		{
			var run = _work.Dequeue();
			run();
		}
	}
}

