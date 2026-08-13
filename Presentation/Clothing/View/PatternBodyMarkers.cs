using System.Collections.Generic;
using DelaunyFabric.Core;
using Godot;

namespace DelaunyFabric.View;

/// <summary>3D view of the same session: pick nodes (select/connect/sew) or gizmo-pose an island. UV is edited in 2D.</summary>
public partial class PatternBodyMarkers : Node3D
{
	public PatternSession Session { get; set; } = new();
	public Camera3D Camera { get; set; }

	readonly List<MeshInstance3D> _nodeDots = [];
	readonly List<Node3D> _islandPivots = [];
	readonly List<MeshInstance3D> _islandHandles = [];
	readonly StandardMaterial3D _matNode = new() { AlbedoColor = new Color(0.75f, 0.8f, 1f) };
	readonly StandardMaterial3D _matNodeSelected = new()
	{
		AlbedoColor = new Color(0.95f, 0.85f, 0.25f),
		EmissionEnabled = true,
		Emission = new Color(0.4f, 0.35f, 0.05f),
	};
	readonly StandardMaterial3D _matLink = new()
	{
		AlbedoColor = new Color(0.3f, 1f, 0.5f),
		EmissionEnabled = true,
		Emission = new Color(0.1f, 0.4f, 0.15f),
	};
	readonly StandardMaterial3D _matHandle = new() { AlbedoColor = new Color(1f, 0.45f, 0.95f) };
	readonly StandardMaterial3D _matHandleSelected = new()
	{
		AlbedoColor = new Color(1f, 0.75f, 0.2f),
		EmissionEnabled = true,
		Emission = new Color(0.45f, 0.3f, 0.05f),
	};

	IkTransformGizmo _gizmo;
	MeshInstance3D _sewLines;
	Node3D _nodePivot;
	bool _wasBusy;
	Transform3D _dragStartPivot;
	readonly Dictionary<int, Vector3> _dragStartWorld = new();
	const float MarkerRadius = 0.012f;
	const float HandleSize = 0.028f;
	readonly StandardMaterial3D _matSew = new()
	{
		AlbedoColor = new Color(1f, 0.55f, 0.35f),
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		NoDepthTest = true,
	};

	GarmentPattern Pattern => Session?.Pattern;
	public bool GizmoBusy => _gizmo != null && _gizmo.Busy;

	public override void _Ready()
	{
		Name = "PatternBodyMarkers";
		_gizmo = new IkTransformGizmo { Name = "IslandGizmo" };
		AddChild(_gizmo);
		_nodePivot = new Node3D { Name = "NodePivot" };
		AddChild(_nodePivot);
		_sewLines = new MeshInstance3D
		{
			Name = "SewLines",
			MaterialOverride = _matSew,
		};
		AddChild(_sewLines);
	}

	public void SetGizmoMode(IkTransformGizmo.GizmoMode mode) =>
		_gizmo?.SetMode(mode);

	public void Rebuild()
	{
		if (GizmoBusy)
			return;

		ClearVisuals();
		if (Pattern?.Nodes == null)
		{
			Session.ClampToPattern();
			BindEditGizmo();
			return;
		}

		Pattern.SyncIslands();
		Session.ClampToPattern();

		for (int i = 0; i < Pattern.Nodes.Count; i++)
		{
			var mesh = new MeshInstance3D
			{
				Name = $"Node_{i}",
				Mesh = new SphereMesh { Radius = MarkerRadius, Height = MarkerRadius * 2f },
				Position = Pattern.NodeWorld(i),
			};
			AddChild(mesh);
			_nodeDots.Add(mesh);
		}

		for (int i = 0; i < Pattern.Islands.Count; i++)
		{
			var pivot = new Node3D
			{
				Name = $"Island_{i}",
				Transform = Pattern.IslandAt(i).Transform,
			};
			var handle = new MeshInstance3D
			{
				Name = $"Handle_{i}",
				Mesh = new BoxMesh { Size = Vector3.One * HandleSize },
			};
			pivot.AddChild(handle);
			AddChild(pivot);
			_islandPivots.Add(pivot);
			_islandHandles.Add(handle);
		}

		RefreshHighlights();
		BindEditGizmo();
		RebuildSewLines();
	}

	public void OnNodeSelectionChanged()
	{
		RefreshHighlights();
	}

	public void RefreshHighlights()
	{
		for (int i = 0; i < _nodeDots.Count; i++)
		{
			if (i == Session.LinkFrom)
				_nodeDots[i].MaterialOverride = _matLink;
			else
				_nodeDots[i].MaterialOverride = Session.IsSelected(i) ? _matNodeSelected : _matNode;
		}

		for (int i = 0; i < _islandHandles.Count; i++)
			_islandHandles[i].MaterialOverride = i == Session.SelectedIsland ? _matHandleSelected : _matHandle;
		if (!GizmoBusy)
			BindEditGizmo();
	}

	public override void _Process(double delta)
	{
		bool busy = GizmoBusy;
		if (busy && !_wasBusy)
			BeginDrag();
		if (busy)
			ApplyDrag();
		if (_wasBusy && !busy)
			EndDrag();
		_wasBusy = busy;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Session?.Pattern == null || GizmoBusy)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Key0 }
		    && Session.Mode == PatternEditMode.Select)
		{
			Session.ZeroSelectionOffset();
			GetViewport()?.SetInputAsHandled();
			return;
		}

		if (Camera == null)
			return;

		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
			return;

		bool shift = mb.ShiftPressed || Input.IsKeyPressed(Key.Shift);
		var origin = Camera.ProjectRayOrigin(mb.Position);
		var dir = Camera.ProjectRayNormal(mb.Position);
		int node = Pick(origin, dir, MarkerRadius * 3f, _nodeDots, out float nodeDepth);
		int island = Pick(origin, dir, HandleSize * 1.4f, _islandHandles, out float islandDepth);
		if (node >= 0 && island >= 0)
		{
			if (islandDepth < nodeDepth)
				node = -1;
			else
				island = -1;
		}

		if (Session.Mode == PatternEditMode.AddNode)
		{
			if (TryAddNodeFromRay(origin, dir))
				GetViewport()?.SetInputAsHandled();
			return;
		}

		if (node >= 0)
		{
			Session.HitNode(node, shift);
			GetViewport()?.SetInputAsHandled();
			return;
		}

		if (island >= 0 && Session.Mode == PatternEditMode.Select)
		{
			Session.HitIsland(island);
			GetViewport()?.SetInputAsHandled();
			return;
		}

		Session.Miss(shift);
		GetViewport()?.SetInputAsHandled();
	}

	bool TryAddNodeFromRay(Vector3 origin, Vector3 dir)
	{
		int island = Session.Selected >= 0 && Session.Selected < Pattern.Nodes.Count
			? Pattern.Nodes[Session.Selected].Island
			: Session.SelectedIsland;
		if (island < 0 && Pattern.Islands.Count > 0)
			island = 0;
		if (island < 0 || island >= Pattern.Islands.Count)
			return false;

		var pose = Pattern.IslandAt(island);
		var hit = new Plane(pose.Outward, pose.Position).IntersectsRay(origin, dir);
		if (hit == null)
			return false;

		var uv = pose.FromWorld(hit.Value, Pattern.WorldScale);
		Session.AddNodeAt(uv);
		return true;
	}

	bool IslandSelected => Session.SelectedIsland >= 0;

	void BeginDrag()
	{
		_dragStartWorld.Clear();
		if (IslandSelected || Pattern == null)
			return;
		foreach (int i in Session.Selection)
		{
			if (i >= 0 && i < Pattern.Nodes.Count)
				_dragStartWorld[i] = Pattern.NodeWorld(i);
		}
		_dragStartPivot = _nodePivot.GlobalTransform;
	}

	void ApplyDrag()
	{
		if (IslandSelected)
		{
			int island = Session.SelectedIsland;
			if (island < 0 || island >= _islandPivots.Count)
				return;
			Pattern.Islands[island].SetTransform(_islandPivots[island].GlobalTransform);
			UpdateIslandNodeDots(island);
			UpdateOutward();
			RebuildSewLines();
			Session.NotifyPatternChanged();
			return;
		}

		ApplyNodeOffset();
	}

	void ApplyNodeOffset()
	{
		if (_dragStartWorld.Count == 0 || Pattern == null)
			return;

		var delta = _nodePivot.GlobalTransform * _dragStartPivot.AffineInverse();
		foreach (var kv in _dragStartWorld)
		{
			Pattern.SetNodeWorld(kv.Key, delta * kv.Value);
			if (kv.Key < _nodeDots.Count)
				_nodeDots[kv.Key].Position = Pattern.NodeWorld(kv.Key);
		}

		RebuildSewLines();
		Session.NotifyPatternChanged();
	}

	void EndDrag()
	{
		if (IslandSelected)
		{
			int island = Session.SelectedIsland;
			if (island >= 0 && island < _islandPivots.Count)
				_islandPivots[island].Transform = Pattern.IslandAt(island).Transform;
		}
		else
			BindEditGizmo();
		_dragStartWorld.Clear();
		Session.NotifyPatternChanged();
	}

	void BindEditGizmo()
	{
		if (_gizmo == null)
			return;
		if (Session.Mode != PatternEditMode.Select)
		{
			_gizmo.Bound = null;
			return;
		}

		int handle = Session.SelectedIsland;
		if (handle >= 0 && handle < _islandPivots.Count)
		{
			_gizmo.Bound = _islandPivots[handle];
			_gizmo.Outward = Pattern.IslandAt(handle).Outward;
			return;
		}

		int islandIdx = IslandOfSelection();
		if (islandIdx < 0 || Session.Selection.Count == 0)
		{
			_gizmo.Bound = null;
			_gizmo.Outward = Vector3.Zero;
			return;
		}

		var island = Pattern.IslandAt(islandIdx);
		PlaceNodePivot(island);
		_gizmo.Bound = _nodePivot;
		_gizmo.Outward = island.Outward;
	}

	void PlaceNodePivot(GarmentIsland island)
	{
		_nodePivot.GlobalPosition = SelectionWorldCentroid();
		_nodePivot.GlobalBasis = new Basis(island.Rotation);
	}

	void UpdateIslandNodeDots(int island)
	{
		for (int i = 0; i < Pattern.Nodes.Count; i++)
		{
			if (Pattern.Nodes[i].Island != island)
				continue;
			if (i < _nodeDots.Count)
				_nodeDots[i].Position = Pattern.NodeWorld(i);
		}
	}

	int IslandOfSelection()
	{
		int i = Session.Selected;
		if (Pattern?.Nodes == null || i < 0 || i >= Pattern.Nodes.Count)
			return -1;
		return Pattern.Nodes[i].Island;
	}

	Vector3 SelectionWorldCentroid()
	{
		var s = Vector3.Zero;
		int n = 0;
		foreach (int i in Session.Selection)
		{
			if (i < 0 || i >= Pattern.Nodes.Count)
				continue;
			s += Pattern.NodeWorld(i);
			n++;
		}
		return n == 0 ? Vector3.Zero : s / n;
	}

	void UpdateOutward()
	{
		if (_gizmo == null)
			return;
		if (IslandSelected && Pattern?.Islands != null && Session.SelectedIsland < Pattern.Islands.Count)
			_gizmo.Outward = Pattern.IslandAt(Session.SelectedIsland).Outward;
		else
		{
			int island = IslandOfSelection();
			_gizmo.Outward = island >= 0 ? Pattern.IslandAt(island).Outward : Vector3.Zero;
		}
	}

	void RebuildSewLines()
	{
		if (_sewLines == null || Pattern?.Sews == null || Pattern.Nodes == null)
			return;

		var verts = new List<Vector3>();
		foreach (Variant v in Pattern.Sews)
		{
			if (v.AsGodotObject() is not GarmentSew s)
				continue;
			if (s.A < 0 || s.B < 0 || s.A >= Pattern.Nodes.Count || s.B >= Pattern.Nodes.Count)
				continue;
			AddDashes(verts, Pattern.NodeWorld(s.A), Pattern.NodeWorld(s.B));
		}

		if (verts.Count < 2)
		{
			_sewLines.Mesh = null;
			return;
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
		_sewLines.Mesh = mesh;
	}

	static void AddDashes(List<Vector3> verts, Vector3 a, Vector3 b, float dash = 0.025f, float gap = 0.018f)
	{
		var d = b - a;
		float len = d.Length();
		if (len < 1e-6f)
			return;
		var dir = d / len;
		float t = 0f;
		while (t < len)
		{
			float t1 = Mathf.Min(t + dash, len);
			verts.Add(a + dir * t);
			verts.Add(a + dir * t1);
			t = t1 + gap;
		}
	}

	static int Pick(Vector3 origin, Vector3 dir, float radius, IReadOnlyList<MeshInstance3D> meshes, out float depth)
	{
		depth = float.MaxValue;
		int hit = -1;
		for (int i = 0; i < meshes.Count; i++)
		{
			var p = meshes[i].GlobalPosition;
			float t = (p - origin).Dot(dir);
			if (t < 0f)
				continue;
			float d = (origin + dir * t).DistanceTo(p);
			if (d < radius && t < depth)
			{
				depth = t;
				hit = i;
			}
		}
		return hit;
	}

	void ClearVisuals()
	{
		foreach (var m in _nodeDots)
		{
			RemoveChild(m);
			m.QueueFree();
		}
		_nodeDots.Clear();
		_islandHandles.Clear();
		foreach (var p in _islandPivots)
		{
			RemoveChild(p);
			p.QueueFree();
		}
		_islandPivots.Clear();
	}
}
