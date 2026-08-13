using System;
using System.Collections.Generic;
using DelaunyFabric.Core;
using Godot;

namespace DelaunyFabric.View;

public enum PatternEditMode
{
	Select,
	AddNode,
	Connect,
	Sew,
}

/// <summary>
/// Shared authoring session. 2D and 3D views only pick and display; graph edits go through here.
/// </summary>
public sealed class PatternSession
{
	public GarmentPattern Pattern { get; set; } = new();
	public bool SnapEnabled { get; set; }
	public int SnapDivisions { get; set; } = 16;

	PatternEditMode _mode = PatternEditMode.Select;
	public PatternEditMode Mode
	{
		get => _mode;
		set
		{
			if (_mode == value)
				return;
			_mode = value;
			CancelLink();
		}
	}

	public int Selected { get; private set; } = -1;
	public int SelectedIsland { get; private set; } = -1;
	public int LinkFrom { get; private set; } = -1;
	public bool IsSelected(int node) => _selection.Contains(node);

	public IReadOnlyCollection<int> Selection => _selection;
	public bool HasClipboard => _clipboard != null && _clipboard.Nodes.Count > 0;

	public event Action SelectionChanged;
	public event Action PatternChanged;

	readonly HashSet<int> _selection = [];
	PatternClipboard _clipboard;

	/// <summary>Click a node in any viewport. Mode decides select / connect / sew.</summary>
	public void NotifyPatternChanged() => PatternChanged?.Invoke();

	public void HitNode(int node, bool shift = false)
	{
		if (Pattern?.Nodes == null || node < 0 || node >= Pattern.Nodes.Count)
			return;

		switch (Mode)
		{
			case PatternEditMode.Connect:
			case PatternEditMode.Sew:
				if (LinkFrom < 0)
				{
					LinkFrom = node;
					SetSelected(node);
					return;
				}

				if (Mode == PatternEditMode.Connect)
					Pattern.ToggleEdge(LinkFrom, node);
				else
					Pattern.ToggleSew(LinkFrom, node);
				LinkFrom = -1;
				SetSelected(node);
				PatternChanged?.Invoke();
				return;

			case PatternEditMode.AddNode:
				SetSelected(node);
				return;

			default:
				if (shift)
					ToggleInSelection(node);
				else if (!_selection.Contains(node))
					SetSelected(node);
				else
				{
					SelectedIsland = -1;
					Selected = node;
					SelectionChanged?.Invoke();
				}
				return;
		}
	}

	public void HitIsland(int island)
	{
		if (Mode != PatternEditMode.Select || Pattern?.Islands == null)
			return;
		if (island < 0 || island >= Pattern.Islands.Count)
			return;
		_selection.Clear();
		Selected = -1;
		LinkFrom = -1;
		SelectedIsland = island;
		SelectionChanged?.Invoke();
	}

	public void Miss(bool shift = false)
	{
		if (Mode is PatternEditMode.Connect or PatternEditMode.Sew)
		{
			CancelLink();
			return;
		}

		if (Mode == PatternEditMode.Select && !shift)
			ClearSelection();
	}

	public void AddNodeAt(Vector2 uv)
	{
		if (Pattern == null)
			return;
		uv = SnapUv(uv);
		int island = Selected >= 0 && Selected < Pattern.Nodes.Count
			? Pattern.Nodes[Selected].Island
			: SelectedIsland;
		int idx = Pattern.AddNode(uv, island);
		SetSelected(idx);
		PatternChanged?.Invoke();
	}

	public void CancelLink()
	{
		if (LinkFrom < 0)
			return;
		LinkFrom = -1;
		SelectionChanged?.Invoke();
	}

	public void SetSelected(int index)
	{
		_selection.Clear();
		SelectedIsland = -1;
		if (index >= 0)
			_selection.Add(index);
		Selected = index;
		SelectionChanged?.Invoke();
	}

	public void SetSelection(IEnumerable<int> indices, int primary = -1)
	{
		_selection.Clear();
		SelectedIsland = -1;
		if (Pattern?.Nodes != null)
		{
			foreach (var i in indices)
			{
				if (i >= 0 && i < Pattern.Nodes.Count)
					_selection.Add(i);
			}
		}

		Selected = primary >= 0 && _selection.Contains(primary)
			? primary
			: (_selection.Count > 0 ? FirstSelected() : -1);
		SelectionChanged?.Invoke();
	}

	public void ClearSelection()
	{
		if (_selection.Count == 0 && Selected < 0 && LinkFrom < 0 && SelectedIsland < 0)
			return;
		_selection.Clear();
		Selected = -1;
		SelectedIsland = -1;
		LinkFrom = -1;
		SelectionChanged?.Invoke();
	}

	public void ToggleInSelection(int node)
	{
		SelectedIsland = -1;
		if (!_selection.Remove(node))
			_selection.Add(node);
		Selected = _selection.Contains(node) ? node : FirstSelected();
		SelectionChanged?.Invoke();
	}

	public void AddToSelection(IEnumerable<int> indices)
	{
		if (Pattern?.Nodes == null)
			return;
		SelectedIsland = -1;
		foreach (var i in indices)
		{
			if (i >= 0 && i < Pattern.Nodes.Count)
				_selection.Add(i);
		}
		Selected = FirstSelected();
		SelectionChanged?.Invoke();
	}

	public void ClampToPattern()
	{
		if (Pattern?.Islands == null || SelectedIsland >= Pattern.Islands.Count)
			SelectedIsland = -1;
		if (Pattern?.Nodes == null)
			return;
		_selection.RemoveWhere(i => i < 0 || i >= Pattern.Nodes.Count);
		if (Selected >= Pattern.Nodes.Count || (Selected >= 0 && !_selection.Contains(Selected)))
			Selected = FirstSelected();
	}

	public void CopySelection()
	{
		_clipboard = CaptureClipboard(_selection);
	}

	public void PasteClipboard(bool mirrorX = false, bool mirrorY = false)
	{
		if (!HasClipboard || Pattern == null)
			return;

		int baseIndex = Pattern.Nodes.Count;
		var clip = _clipboard;
		var centroid = ClipboardCentroid(clip);
		int island = Pattern.AddIsland(clip.Island);
		var newSelection = new List<int>();
		for (int i = 0; i < clip.Nodes.Count; i++)
		{
			var uv = MirrorUv(clip.Nodes[i], centroid, mirrorX, mirrorY);
			if (!mirrorX && !mirrorY)
				uv = SnapUv(uv + new Vector2(1f / Mathf.Max(2, SnapDivisions), 0f));
			else
				uv = SnapUv(uv);
			int idx = Pattern.AddNode(uv, island);
			var off = i < clip.Offsets.Count ? clip.Offsets[i] : Vector3.Zero;
			if (mirrorX)
				off.X = -off.X;
			if (mirrorY)
				off.Y = -off.Y;
			Pattern.Nodes[idx].Offset = off;
			newSelection.Add(idx);
		}

		foreach (var (a, b) in clip.Edges)
			Pattern.AddEdge(baseIndex + a, baseIndex + b, sync: false);
		foreach (var (a, b) in clip.Sews)
			Pattern.AddSew(baseIndex + a, baseIndex + b);
		Pattern.SyncIslands();
		SetSelection(newSelection, newSelection.Count > 0 ? newSelection[0] : -1);
		PatternChanged?.Invoke();
	}

	public void ZeroSelectionOffset()
	{
		if (Pattern == null || _selection.Count == 0)
			return;
		foreach (int i in _selection)
		{
			if (i < 0 || i >= Pattern.Nodes.Count)
				continue;
			Pattern.Nodes[i].Offset = Vector3.Zero;
		}
		PatternChanged?.Invoke();
	}

	public void DeleteSelection()
	{
		if (Pattern == null || _selection.Count == 0)
			return;
		var ordered = new List<int>(_selection);
		ordered.Sort();
		for (int i = ordered.Count - 1; i >= 0; i--)
			Pattern.RemoveNode(ordered[i]);
		ClearSelection();
		PatternChanged?.Invoke();
	}

	public Vector2 SnapUv(Vector2 uv)
	{
		if (!SnapEnabled || SnapDivisions <= 1)
			return new Vector2(Mathf.Clamp(uv.X, 0f, 1f), Mathf.Clamp(uv.Y, 0f, 1f));
		float step = 1f / SnapDivisions;
		return new Vector2(
			Mathf.Clamp(Mathf.Round(uv.X / step) * step, 0f, 1f),
			Mathf.Clamp(Mathf.Round(uv.Y / step) * step, 0f, 1f));
	}

	int FirstSelected()
	{
		int best = int.MaxValue;
		foreach (int i in _selection)
			if (i < best) best = i;
		return best == int.MaxValue ? -1 : best;
	}

	PatternClipboard CaptureClipboard(HashSet<int> indices)
	{
		var list = new List<int>(indices);
		list.Sort();
		if (list.Count == 0 || Pattern == null)
			return null;

		var map = new Dictionary<int, int>();
		var nodes = new List<Vector2>();
		var offsets = new List<Vector3>();
		for (int i = 0; i < list.Count; i++)
		{
			map[list[i]] = i;
			nodes.Add(Pattern.Nodes[list[i]].Uv);
			offsets.Add(Pattern.Nodes[list[i]].Offset);
		}

		var edges = new List<(int, int)>();
		foreach (Variant v in Pattern.Edges)
		{
			if (v.AsGodotObject() is not GarmentEdge e)
				continue;
			if (!map.TryGetValue(e.A, out int a) || !map.TryGetValue(e.B, out int b))
				continue;
			edges.Add(a < b ? (a, b) : (b, a));
		}

		var sews = new List<(int, int)>();
		foreach (Variant v in Pattern.Sews)
		{
			if (v.AsGodotObject() is not GarmentSew s)
				continue;
			if (!map.TryGetValue(s.A, out int a) || !map.TryGetValue(s.B, out int b))
				continue;
			sews.Add(a < b ? (a, b) : (b, a));
		}

		return new PatternClipboard(
			nodes,
			offsets,
			edges,
			sews,
			Pattern.IslandAt(Pattern.Nodes[list[0]].Island).DuplicatePose());
	}

	static Vector2 ClipboardCentroid(PatternClipboard clip)
	{
		var c = Vector2.Zero;
		foreach (var uv in clip.Nodes)
			c += uv;
		return c / clip.Nodes.Count;
	}

	static Vector2 MirrorUv(Vector2 uv, Vector2 centroid, bool mirrorX, bool mirrorY)
	{
		if (mirrorX)
			uv.X = 2f * centroid.X - uv.X;
		if (mirrorY)
			uv.Y = 2f * centroid.Y - uv.Y;
		return uv;
	}

	sealed class PatternClipboard(
		List<Vector2> nodes,
		List<Vector3> offsets,
		List<(int, int)> edges,
		List<(int, int)> sews,
		GarmentIsland island)
	{
		public List<Vector2> Nodes { get; } = nodes;
		public List<Vector3> Offsets { get; } = offsets;
		public List<(int, int)> Edges { get; } = edges;
		public List<(int, int)> Sews { get; } = sews;
		public GarmentIsland Island { get; } = island;
	}
}
