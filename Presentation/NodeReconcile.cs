using System;
using System.Collections.Generic;
using Godot;

/// <summary>Reconcile a parent’s children by stable key — create / update / remove, no full wipe.</summary>
public static class NodeReconcile
{
	public static void Sync<TKey>(
		Node parent,
		Dictionary<TKey, Node> map,
		IEnumerable<TKey> desired,
		Func<TKey, Node> create,
		Action<TKey, Node> update)
	{
		var want = desired as ISet<TKey> ?? new HashSet<TKey>(desired);

		List<TKey> remove = null;
		foreach (var kv in map)
		{
			if (want.Contains(kv.Key))
				continue;
			(remove ??= new List<TKey>()).Add(kv.Key);
			parent.RemoveChild(kv.Value);
			kv.Value.QueueFree();
		}
		if (remove != null)
		{
			foreach (var k in remove)
				map.Remove(k);
		}

		foreach (var key in want)
		{
			if (!map.TryGetValue(key, out var node) || !GodotObject.IsInstanceValid(node))
			{
				node = create(key);
				parent.AddChild(node);
				map[key] = node;
			}
			update(key, node);
		}
	}
}
