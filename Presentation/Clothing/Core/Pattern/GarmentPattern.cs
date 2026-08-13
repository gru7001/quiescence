using Godot;
using System;
using System.Collections.Generic;

namespace DelaunyFabric.Core;

/// <summary>Saveable garment authoring: UV graph + island 3D poses + sews.</summary>
[GlobalClass]
public partial class GarmentPattern : Godot.Resource
{
	[Export] public Godot.Collections.Array<GarmentNode> Nodes { get; set; } = [];
	[Export] public Godot.Collections.Array<GarmentEdge> Edges { get; set; } = [];
	[Export] public Godot.Collections.Array<GarmentSew> Sews { get; set; } = [];
	[Export] public Godot.Collections.Array<GarmentIsland> Islands { get; set; } = [];
	/// <summary>World rest-length scale: edge length ≈ UV distance × UvScale.</summary>
	[Export] public float UvScale { get; set; } = 0.5f;

	public float WorldScale => UvScale > 1e-8f ? UvScale : 0.5f;

	public Vector3 NodeWorld(int index)
	{
		if (index < 0 || index >= Nodes.Count)
			return Vector3.Zero;
		var n = Nodes[index];
		var island = IslandAt(n.Island);
		return island.ToWorld(n.Uv, WorldScale) + new Basis(island.Rotation) * n.Offset;
	}

	public void SetNodeWorld(int index, Vector3 world)
	{
		if (index < 0 || index >= Nodes.Count)
			return;
		var n = Nodes[index];
		var island = IslandAt(n.Island);
		var rest = island.ToWorld(n.Uv, WorldScale);
		n.Offset = new Basis(island.Rotation).Inverse() * (world - rest);
	}

	Vector2 CentroidUv(IReadOnlyList<int> nodes)
	{
		var s = Vector2.Zero;
		int n = 0;
		foreach (int i in nodes)
		{
			if (i < 0 || i >= Nodes.Count)
				continue;
			s += Nodes[i].Uv;
			n++;
		}
		return n == 0 ? new Vector2(0.5f, 0.5f) : s / n;
	}

	public Vector2 IslandUvCentroid(int island)
	{
		var s = Vector2.Zero;
		int n = 0;
		foreach (Variant v in Nodes)
		{
			if (v.AsGodotObject() is not GarmentNode node || node.Island != island)
				continue;
			s += node.Uv;
			n++;
		}
		return n == 0 ? Vector2.Zero : s / n;
	}

	public GarmentIsland IslandAt(int index)
	{
		if (index >= 0 && index < Islands.Count && Islands[index] != null)
			return Islands[index];
		return GarmentIsland.Default();
	}

	public int AddIsland(GarmentIsland pose = null)
	{
		Islands.Add(pose != null ? pose.DuplicatePose() : GarmentIsland.Default());
		return Islands.Count - 1;
	}

	public int AddNode(Vector2 uv, int island = -1)
	{
		if (island < 0 || island >= Islands.Count)
			island = AddIsland();
		Nodes.Add(new GarmentNode { Uv = uv, Island = island });
		return Nodes.Count - 1;
	}

	public void AddEdge(int a, int b, bool sync = true)
	{
		if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count)
			return;
		if (a > b) (a, b) = (b, a);
		foreach (Variant v in Edges)
		{
			if (v.AsGodotObject() is GarmentEdge e && e.A == a && e.B == b)
				return;
		}
		Edges.Add(new GarmentEdge { A = a, B = b });
		if (sync)
			SyncIslands();
	}

	public void ToggleEdge(int a, int b)
	{
		if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count)
			return;
		if (a > b) (a, b) = (b, a);
		var next = new Godot.Collections.Array<GarmentEdge>();
		bool removed = false;
		foreach (Variant v in Edges)
		{
			if (v.AsGodotObject() is not GarmentEdge e)
				continue;
			if (e.A == a && e.B == b)
			{
				removed = true;
				continue;
			}
			next.Add(e);
		}

		if (removed)
			Edges = next;
		else
			Edges.Add(new GarmentEdge { A = a, B = b });
		SyncIslands();
	}

	public void AddSew(int a, int b)
	{
		if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count)
			return;
		if (a > b) (a, b) = (b, a);
		foreach (Variant v in Sews)
		{
			if (v.AsGodotObject() is GarmentSew s && s.A == a && s.B == b)
				return;
		}
		Sews.Add(new GarmentSew { A = a, B = b });
	}

	public void ToggleSew(int a, int b)
	{
		if (a == b || a < 0 || b < 0 || a >= Nodes.Count || b >= Nodes.Count)
			return;
		if (a > b) (a, b) = (b, a);
		var next = new Godot.Collections.Array<GarmentSew>();
		bool removed = false;
		foreach (Variant v in Sews)
		{
			if (v.AsGodotObject() is not GarmentSew s)
				continue;
			if (s.A == a && s.B == b)
			{
				removed = true;
				continue;
			}
			next.Add(s);
		}

		if (removed)
			Sews = next;
		else
			Sews.Add(new GarmentSew { A = a, B = b });
	}

	public void RemoveNode(int index)
	{
		if (index < 0 || index >= Nodes.Count)
			return;
		Nodes.RemoveAt(index);

		var nextEdges = new Godot.Collections.Array<GarmentEdge>();
		foreach (Variant v in Edges)
		{
			if (v.AsGodotObject() is not GarmentEdge e)
				continue;
			if (e.A == index || e.B == index)
				continue;
			nextEdges.Add(new GarmentEdge
			{
				A = e.A > index ? e.A - 1 : e.A,
				B = e.B > index ? e.B - 1 : e.B,
			});
		}
		Edges = nextEdges;

		var nextSews = new Godot.Collections.Array<GarmentSew>();
		foreach (Variant v in Sews)
		{
			if (v.AsGodotObject() is not GarmentSew s)
				continue;
			if (s.A == index || s.B == index)
				continue;
			nextSews.Add(new GarmentSew
			{
				A = s.A > index ? s.A - 1 : s.A,
				B = s.B > index ? s.B - 1 : s.B,
			});
		}
		Sews = nextSews;
		SyncIslands();
	}

	/// <summary>
	/// One island pose per edge-connected UV component. Merges when edges join,
	/// splits (copied pose) when a component separates.
	/// </summary>
	public void SyncIslands()
	{
		var components = EdgeIslands();
		if (components.Count == 0)
		{
			Islands.Clear();
			return;
		}

		var used = new bool[Islands.Count];
		var next = new Godot.Collections.Array<GarmentIsland>();
		var map = new int[Nodes.Count];

		foreach (var comp in components)
		{
			int src = -1;
			foreach (int i in comp)
			{
				int id = Nodes[i].Island;
				if (id >= 0 && id < Islands.Count && !used[id])
				{
					src = id;
					break;
				}
			}

			GarmentIsland pose;
			if (src >= 0)
			{
				used[src] = true;
				pose = Islands[src].DuplicatePose();
			}
			else
			{
				int any = Nodes[comp[0]].Island;
				pose = any >= 0 && any < Islands.Count
					? Islands[any].DuplicatePose()
					: GarmentIsland.Default();
				if (any < 0 || any >= Islands.Count)
					pose.UvOrigin = CentroidUv(comp);
			}

			int newId = next.Count;
			next.Add(pose);
			foreach (int i in comp)
				map[i] = newId;
		}

		Islands = next;
		for (int i = 0; i < Nodes.Count; i++)
			Nodes[i].Island = map[i];
		RecenterAllIslands();
	}

	/// <summary>
	/// Put the handle at the node centroid without moving any world points
	/// (UvOrigin := UV mean, Position := world mean).
	/// </summary>
	public void RecenterAllIslands()
	{
		for (int i = 0; i < Islands.Count; i++)
			RecenterIsland(i);
	}

	public void RecenterIsland(int island)
	{
		if (island < 0 || island >= Islands.Count || Islands[island] == null)
			return;

		var uvC = Vector2.Zero;
		var worldC = Vector3.Zero;
		int n = 0;
		for (int i = 0; i < Nodes.Count; i++)
		{
			if (Nodes[i].Island != island)
				continue;
			uvC += Nodes[i].Uv;
			worldC += Islands[island].ToWorld(Nodes[i].Uv, WorldScale);
			n++;
		}

		if (n == 0)
			return;

		Islands[island].UvOrigin = uvC / n;
		Islands[island].Position = worldC / n;
	}

	public static void Save(GarmentPattern data, string path)
	{
		var toSave = Clone(data);
		toSave.TakeOverPath(path);
		Error err = ResourceSaver.Save(toSave, path);
		if (err != Error.Ok)
			throw new InvalidOperationException($"GarmentPattern.Save failed ({err}): {path}");
	}

	public static GarmentPattern Load(string path)
	{
		var loaded = ResourceLoader.Load<GarmentPattern>(
			path, cacheMode: ResourceLoader.CacheMode.Ignore);
		if (loaded == null)
			throw new InvalidOperationException($"GarmentPattern.Load failed: {path}");
		return Clone(loaded);
	}

	public static GarmentPattern Clone(GarmentPattern src)
	{
		var copy = new GarmentPattern();
		if (src == null)
			return copy;

		copy.UvScale = src.UvScale > 1e-8f ? src.UvScale : 0.5f;

		foreach (Variant v in src.Islands ?? [])
		{
			if (v.AsGodotObject() is GarmentIsland island)
				copy.Islands.Add(island.DuplicatePose());
		}
		foreach (Variant v in src.Nodes ?? [])
		{
			if (v.AsGodotObject() is GarmentNode n)
				copy.Nodes.Add(new GarmentNode { Uv = n.Uv, Island = n.Island, Offset = n.Offset });
		}
		foreach (Variant v in src.Edges ?? [])
		{
			if (v.AsGodotObject() is GarmentEdge e)
				copy.Edges.Add(new GarmentEdge { A = e.A, B = e.B });
		}
		foreach (Variant v in src.Sews ?? [])
		{
			if (v.AsGodotObject() is GarmentSew s)
				copy.Sews.Add(new GarmentSew { A = s.A, B = s.B });
		}
		copy.SyncIslands();
		return copy;
	}

	/// <summary>Connected components via panel edges (sews do not join islands).</summary>
	public List<List<int>> EdgeIslands()
	{
		int n = Nodes?.Count ?? 0;
		var adj = new List<int>[n];
		for (int i = 0; i < n; i++)
			adj[i] = [];

		foreach (Variant v in Edges ?? [])
		{
			if (v.AsGodotObject() is not GarmentEdge e)
				continue;
			if (e.A < 0 || e.B < 0 || e.A >= n || e.B >= n || e.A == e.B)
				continue;
			adj[e.A].Add(e.B);
			adj[e.B].Add(e.A);
		}

		var seen = new bool[n];
		var islands = new List<List<int>>();
		for (int i = 0; i < n; i++)
		{
			if (seen[i])
				continue;
			var island = new List<int>();
			var stack = new Stack<int>();
			stack.Push(i);
			seen[i] = true;
			while (stack.Count > 0)
			{
				int u = stack.Pop();
				island.Add(u);
				foreach (int w in adj[u])
				{
					if (seen[w])
						continue;
					seen[w] = true;
					stack.Push(w);
				}
			}
			islands.Add(island);
		}
		return islands;
	}

	/// <summary>Seed a unit-square panel (four corners) for empty patterns.</summary>
	public static GarmentPattern CreateDefaultSquare()
	{
		var p = new GarmentPattern();
		p.AddIsland();
		p.AddNode(new Vector2(0.2f, 0.2f), 0);
		p.AddNode(new Vector2(0.8f, 0.2f), 0);
		p.AddNode(new Vector2(0.8f, 0.8f), 0);
		p.AddNode(new Vector2(0.2f, 0.8f), 0);
		p.AddEdge(0, 1);
		p.AddEdge(1, 2);
		p.AddEdge(2, 3);
		p.AddEdge(3, 0);
		return p;
	}
}
