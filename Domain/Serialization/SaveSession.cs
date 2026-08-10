using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;

/// <summary>
/// One save pass that assigns ids on-demand for referenced objects and queues newly-seen objects
/// for serialization. Intended for graph-style saves where action states can reference bodies/boards/etc.
/// </summary>
public sealed class SaveSession
{
	private sealed class RefEq : IEqualityComparer<object>
	{
		public static readonly RefEq Instance = new();
		public new bool Equals(object x, object y) => ReferenceEquals(x, y);
		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}

	public readonly SaveContext Context;
	private readonly JsonSerializerOptions _json;
	private readonly Dictionary<object, string> _idOf = new(RefEq.Instance);
	private readonly Queue<Action> _work = new();

	private int _nextNode;

	private readonly List<SaveRecord> _nodes = new();

	public SaveSession(SaveContext context = null, JsonSerializerOptions json = null)
	{
		Context = context ?? SaveContext.Default;
		_json = json ?? SaveJson.Options;
	}

	/// <summary>Graph identity uses the runtime object (e.g. concrete <see cref="IDriver"/>), not the <see cref="ISaveable"/> interface type.</summary>
	public NodeRef Ref(ISaveable node)
	{
		if (node == null) throw new ArgumentNullException(nameof(node));
		var key = (object)node;
		if (_idOf.TryGetValue(key, out var id))
			return new NodeRef(id);
		id = $"n{_nextNode++}";
		_idOf.Add(key, id);
		_work.Enqueue(() =>
		{
			var sn = node.SaveTo(this);
			Add(id, sn);
		});
		return new NodeRef(id);
	}

	/// <summary>
	/// Like <see cref="Ref"/>, but allows null by returning <see cref="NodeRefs.Null"/>.
	/// </summary>
	public NodeRef RefOrNull(ISaveable node) => node == null ? NodeRefs.Null : Ref(node);

	/// <summary>
	/// After <see cref="Finish"/> (or any time after queued encodes have <see cref="Drain"/>ed), returns the id assigned to
	/// <paramref name="node"/> if it appeared in this save graph; otherwise <see cref="NodeRefs.Null"/>.
	/// </summary>
	public NodeRef LookupRefOrNull(ISaveable node) =>
		_idOf.TryGetValue(node, out var id) ? new NodeRef(id) : NodeRefs.Null;

	private void Add(string id, SaveNode node)
	{
		if (id == null) throw new ArgumentNullException(nameof(id));
		if (node.Tag == null) throw new ArgumentNullException(nameof(node.Tag));
		if (node.Record == null) throw new ArgumentNullException(nameof(node.Record));

		var json = JsonSerializer.SerializeToElement(node.Record, node.Record.GetType(), _json);
		_nodes.Add(new SaveRecord(Tag: node.Tag, Id: id, Record: json));
	}

	/// <summary>Finalize the save after the world has seeded roots (via Ref calls).</summary>
	public SaveFile Finish(string gameRootId)
	{
		Drain();
		return new SaveFile(GameRootId: gameRootId, Nodes: _nodes);
	}

	private void Drain()
	{
		while (_work.Count > 0)
		{
			var run = _work.Dequeue();
			run();
		}
	}
}
