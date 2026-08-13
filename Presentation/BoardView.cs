using System;
using System.Collections.Generic;
using Godot;

public enum BoardLens
{
	/// <summary>Board picks nothing.</summary>
	None,
	Tile,
	Occupant,
	/// <summary>Viewport split into four equal regions → <see cref="Direction"/>.</summary>
	Direction
}

public sealed record BoardModel(
	Dictionary<Tile, TileCoord> Tiles,
	Dictionary<Tile, IOccupant> Occupants,
	/// <summary>Character footing on the board plane (y = 0), used by the direction lens.</summary>
	Vector3 Footing,
	/// <summary>Sim clock at projection time (drives in-flight body lerp).</summary>
	long Now);

/// <summary>
/// Board surface with lenses (none / tile / occupant / direction).
/// Direction lens: click maps to Up/Right/Down/Left by which of four equal screen regions contains the mouse.
/// </summary>
public partial class BoardView : Node3D
{
	private readonly ExecutionContext _ui;
	private readonly Node3D _tilesRoot;
	private readonly Node3D _bodiesRoot;
	private readonly Camera3D _camera;
	private readonly PackedScene _qbodyScene;
	private readonly Key<BoardModel> _model = new();
	private readonly Key<BoardLens> _lens = new();
	private readonly Key<int> _filterEpoch = new();
	private readonly Dictionary<Tile, Node> _tileNodes = new();
	private readonly Dictionary<Body, Node> _bodyNodes = new();
	private readonly Dictionary<ulong, Tile> _pickToTile = new();
	private readonly StandardMaterial3D _matTile = new() { AlbedoColor = new Color(0.45f, 0.55f, 0.85f) };
	private readonly StandardMaterial3D _matStorage = new() { AlbedoColor = new Color(0.85f, 0.35f, 0.30f) };
	private readonly StandardMaterial3D _matHighlight = new()
	{
		AlbedoColor = new Color(0.95f, 0.9f, 0.3f),
		EmissionEnabled = true,
		Emission = new Color(0.6f, 0.55f, 0.1f)
	};

	private Func<object, bool> _tileFilter;
	private Func<object, bool> _occupantFilter;
	private Func<object, bool> _directionFilter;
	private int _epoch;
	private HashSet<object> _highlighted = new();
	private IkTrackAnimation _walk;
	private bool _walkBakeAttempted;

	public float TileSize { get; set; } = 1f;
	public float TileHeight { get; set; } = 0.2f;
	public float TileGap { get; set; } = 0.05f;
	public float BodyScale { get; set; } = 0.85f;

	public ISelectionInput TileSelector { get; }
	public ISelectionInput OccupantSelector { get; }
	public ISelectionInput DirectionSelector { get; }

	public event Action<Tile> TilePressed;
	public event Action<IOccupant> OccupantPressed;
	public event Action<Direction> DirectionPressed;

	public BoardView(ExecutionContext ui, Node parent)
	{
		_ui = ui;
		Name = "Board";
		_tilesRoot = new Node3D { Name = "Tiles" };
		AddChild(_tilesRoot);
		_bodiesRoot = new Node3D { Name = "Bodies" };
		AddChild(_bodiesRoot);
		_qbodyScene = ResourceLoader.Load<PackedScene>("res://Assets/qbody.glb");

		var camPos = new Vector3(8f, 12f, 8f);
		_camera = new Camera3D
		{
			Name = "BoardCamera",
			Projection = Camera3D.ProjectionType.Perspective,
			Fov = 50f,
			Current = true,
			Position = camPos
		};
		_camera.LookAtFromPosition(camPos, Vector3.Zero);
		AddChild(_camera);

		AddChild(new BoardInput { Board = this });

		TileSelector = new LensInput(this, BoardLens.Tile, ParameterPredicates.Tile[ISelectionInput.Slot]);
		OccupantSelector = new LensInput(this, BoardLens.Occupant, ParameterPredicates.Occupant[ISelectionInput.Slot]);
		DirectionSelector = new LensInput(this, BoardLens.Direction, ParameterPredicates.Direction[ISelectionInput.Slot]);

		parent.AddChild(this);
		_ui.Write(_lens, BoardLens.None);
	}

	public void SetModel(BoardModel model) =>
		_ui.Write(_model, model);

	public void SetLens(BoardLens lens) =>
		_ui.Write(_lens, lens);

	public BoardLens ReadLens() => _ui.Read(_lens);

	public void SetHighlighted(IEnumerable<object> values)
	{
		_highlighted = values == null ? new HashSet<object>() : new HashSet<object>(values);
		_ui.Write(_filterEpoch, ++_epoch);
	}

	public void Render()
	{
		_ = _ui.Read(_filterEpoch);
		var model = _ui.Read(_model);
		_ = _ui.Read(_lens);
		var tiles = model?.Tiles ?? new Dictionary<Tile, TileCoord>();
		var occ = model?.Occupants ?? new Dictionary<Tile, IOccupant>();
		var step = TileSize + TileGap;

		_pickToTile.Clear();
		NodeReconcile.Sync(
			_tilesRoot,
			_tileNodes,
			tiles.Keys,
			tile =>
			{
				var mesh = new MeshInstance3D
				{
					Mesh = new BoxMesh { Size = new Vector3(TileSize, TileHeight, TileSize) }
				};
				mesh.CreateTrimeshCollision();
				return mesh;
			},
			(tile, node) =>
			{
				var mesh = (MeshInstance3D)node;
				var c = tiles[tile];
				mesh.Position = new Vector3(c.Col * step, TileHeight * 0.5f, c.Row * step);
				occ.TryGetValue(tile, out var occupant);
				var hl = _highlighted.Contains(tile)
					|| (occupant != null && _highlighted.Contains(occupant));
				mesh.MaterialOverride = hl ? _matHighlight
					: occupant is Storage ? _matStorage
					: _matTile;
				RegisterPick(mesh, tile);
			});

		var bodies = new HashSet<Body>();
		foreach (var kv in occ)
		{
			if (kv.Value is Body body)
				bodies.Add(body);
		}

		EnsureWalkBaked();

		NodeReconcile.Sync(
			_bodiesRoot,
			_bodyNodes,
			bodies,
			_ => CreateBodyNode(),
			(body, node) =>
			{
				if (node is not Node3D n3d)
					return;
				var now = model?.Now ?? 0L;
				var p = BoardBodyPlacement.OnPlane(body, tiles, step, now);
				n3d.Scale = Vector3.One * (BodyScale * TileSize);
				n3d.Position = new Vector3(p.X, TileHeight, p.Z);
				ApplyBodyMotion(body, n3d, tiles, step, now);
			});
	}

	private void EnsureWalkBaked()
	{
		if (_walkBakeAttempted)
			return;
		_walkBakeAttempted = true;

		try
		{
			if (ResourceLoader.Exists(QbodyIk.WalkTrackPath))
			{
				_walk = IkTrackAnimation.Load(QbodyIk.WalkTrackPath);
				return;
			}

			if (_qbodyScene == null || !ResourceLoader.Exists(QbodyIk.WalkPath))
				return;

			var bakeRoot = _qbodyScene.Instantiate<Node3D>();
			bakeRoot.Visible = false;
			AddChild(bakeRoot);
			try
			{
				var sk = QbodyIk.FindSkeleton(bakeRoot);
				if (sk == null)
					return;
				var rig = QbodyIk.Configure(sk);
				var anim = IkAnimation.Load(QbodyIk.WalkPath);
				_walk = IkTrackAnimation.Bake(anim, rig, QbodyIk.DefaultTerms(), steps: 200);
				IkTrackAnimation.Save(_walk, QbodyIk.WalkTrackPath);
			}
			finally
			{
				RemoveChild(bakeRoot);
				bakeRoot.QueueFree();
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"BoardView: failed to load/bake walk track: {ex.Message}");
			_walk = null;
		}
	}

	private void ApplyBodyMotion(
		Body body,
		Node3D n3d,
		Dictionary<Tile, TileCoord> tiles,
		float step,
		long now)
	{
		var sk = QbodyIk.FindSkeleton(n3d);
		var moving = BoardBodyPlacement.TryMoveEndpoints(body, tiles, step, out var from, out var to);
		if (moving)
		{
			var delta = to - from;
			if (delta.LengthSquared() > 1e-8f)
				n3d.Rotation = new Vector3(0f, Mathf.Atan2(-delta.X, -delta.Z), 0f);
		}

		if (sk == null)
			return;

		if (moving && _walk != null)
		{
			var u = BoardBodyPlacement.MoveProgress(body, now);
			_walk.PlayAt(sk, u * _walk.Duration);
		}
		else
			QbodyIk.ApplyRest(sk);
	}

	private Node3D CreateBodyNode()
	{
		if (_qbodyScene != null)
			return _qbodyScene.Instantiate<Node3D>();
		return new MeshInstance3D
		{
			Mesh = new CapsuleMesh { Radius = 0.25f, Height = 0.8f },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.75f, 0.9f) }
		};
	}

	internal void HandlePick(Vector2 screenPos)
	{
		var lens = _ui.Read(_lens);
		if (lens == BoardLens.None)
			return;

		if (lens == BoardLens.Direction)
		{
			if (!TryDirectionFromClick(screenPos, out var d))
				return;
			if (_directionFilter != null && !_directionFilter(d))
				return;
			DirectionPressed?.Invoke(d);
			return;
		}

		if (!TryPickTile(screenPos, out var tile))
			return;

		var model = _ui.Read(_model);
		if (lens == BoardLens.Tile)
		{
			if (_tileFilter != null && !_tileFilter(tile))
				return;
			TilePressed?.Invoke(tile);
			return;
		}

		if (lens != BoardLens.Occupant)
			return;

		IOccupant occupant = null;
		model?.Occupants?.TryGetValue(tile, out occupant);
		if (occupant == null)
			return;
		if (_occupantFilter != null && !_occupantFilter(occupant))
			return;
		OccupantPressed?.Invoke(occupant);
	}

	/// <summary>
	/// Project click onto y = 0, then take the dominant axis of the offset from
	/// <see cref="BoardModel.Footing"/> (board: +X = Right, +Z = Down).
	/// </summary>
	private bool TryDirectionFromClick(Vector2 screenPos, out Direction direction)
	{
		direction = Direction.Up;
		if (!TryIntersectPlaneY0(screenPos, out var hit))
			return false;

		var model = _ui.Read(_model);
		var stand = model?.Footing ?? Vector3.Zero;
		var dx = hit.X - stand.X;
		var dz = hit.Z - stand.Z;
		if (Mathf.IsZeroApprox(dx) && Mathf.IsZeroApprox(dz))
			return false;

		if (Mathf.Abs(dx) > Mathf.Abs(dz))
			direction = dx > 0f ? Direction.Right : Direction.Left;
		else
			direction = dz > 0f ? Direction.Down : Direction.Up;
		return true;
	}

	private bool TryIntersectPlaneY0(Vector2 screenPos, out Vector3 hit)
	{
		hit = default;
		var origin = _camera.ProjectRayOrigin(screenPos);
		var dir = _camera.ProjectRayNormal(screenPos);
		if (Mathf.IsZeroApprox(dir.Y))
			return false;
		var t = -origin.Y / dir.Y;
		if (t < 0f)
			return false;
		hit = origin + dir * t;
		hit.Y = 0f;
		return true;
	}

	private void RegisterPick(Node root, Tile tile)
	{
		if (!GodotObject.IsInstanceValid(root))
			return;
		_pickToTile[root.GetInstanceId()] = tile;
		foreach (var child in root.GetChildren())
		{
			if (child is Node n)
				RegisterPick(n, tile);
		}
	}

	private bool TryPickTile(Vector2 screenPos, out Tile tile)
	{
		tile = null;
		var world = GetWorld3D();
		var state = world?.DirectSpaceState;
		if (state == null)
			return false;

		var origin = _camera.ProjectRayOrigin(screenPos);
		var dir = _camera.ProjectRayNormal(screenPos);
		var hit = state.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, origin + dir * 10_000f));
		if (hit == null || hit.Count == 0)
			return false;
		if (!hit.TryGetValue("collider", out var colliderObj))
			return false;

		var node = colliderObj.AsGodotObject() as Node;
		while (node != null && GodotObject.IsInstanceValid(node))
		{
			if (_pickToTile.TryGetValue(node.GetInstanceId(), out tile) && tile != null)
				return true;
			node = node.GetParent();
		}
		return false;
	}

	private sealed partial class BoardInput : Node
	{
		public BoardView Board;

		public override void _UnhandledInput(InputEvent @event)
		{
			if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
				return;

			var lens = Board.ReadLens();
			if (lens == BoardLens.None)
				return;

			if (lens == BoardLens.Direction)
			{
				Board.HandlePick(mb.Position);
				GetViewport()?.SetInputAsHandled();
				return;
			}

			if (!Board.TryPickTile(mb.Position, out _))
				return;
			Board.HandlePick(mb.Position);
			GetViewport()?.SetInputAsHandled();
		}
	}

	private sealed class LensInput : ISelectionInput
	{
		private readonly BoardView _board;
		private readonly BoardLens _lens;

		public Formula Guarantee { get; }
		public bool IsBoardLens => true;
		public FloatingPanel Panel => null;
		public Var PromptedHole { get; private set; }

		public Func<object, bool> CandidateFilter
		{
			get => FilterRef();
			set
			{
				switch (_lens)
				{
					case BoardLens.Tile:
						_board._tileFilter = value;
						break;
					case BoardLens.Occupant:
						_board._occupantFilter = value;
						break;
					case BoardLens.Direction:
						_board._directionFilter = value;
						break;
				}
				_board._ui.Write(_board._filterEpoch, ++_board._epoch);
			}
		}

		public LensInput(BoardView board, BoardLens lens, Formula guarantee)
		{
			_board = board;
			_lens = lens;
			Guarantee = guarantee;
		}

		public void Prompt(Var hole)
		{
			PromptedHole = hole;
			_board.SetLens(_lens);
		}

		public void ClearPrompt()
		{
			PromptedHole = null;
			CandidateFilter = null;
		}

		private Func<object, bool> FilterRef() => _lens switch
		{
			BoardLens.Tile => _board._tileFilter,
			BoardLens.Occupant => _board._occupantFilter,
			BoardLens.Direction => _board._directionFilter,
			_ => null
		};
	}
}
