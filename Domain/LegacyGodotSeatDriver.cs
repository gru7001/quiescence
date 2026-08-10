using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Legacy human control seat (full HUD / procedural board). Prefer <see cref="GodotSeatDriver"/> in Presentation.
/// </summary>
public sealed partial class LegacyGodotSeatDriver : IDriver, ISaveable<LegacyGodotSeatDriverPersistence.DriverSave>
{
	private readonly Node _seatRoot;
	private readonly Control _uiRoot;
	private Func<CommandDefinition, Assignment, bool> _submit = null!;
	private VBoxContainer _boardPanel = null!;
	private Node3D _board3dRoot = null!;
	private SeatBoard3D _board3d = null!;
	private SeatInputNode _inputNode = null!;
	private PopupMenu _contextMenu = null!;
	private List<(CommandDefinition Cmd, Assignment VariableAssignment)> _contextMenuOptions = null!;

	// UI reactive engine (non-temporal): ExecutionContext + ReactiveEngine.
	private readonly ExecutionContext _uiCtx;
	private readonly ReactiveEngine _ui;
	private readonly Key<bool> _uiHasPendingDecision = new();
	private readonly Key<Body> _uiVehicle = new();
	private readonly Key<CommandDefinition> _uiSelectedCommand = new();
	private readonly Key<Item> _uiSelectedItem = new();
	private readonly Key<Assignment> _uiPartialAssignment = new();
	private readonly Key<Tile> _uiInspectTile = new();
	private readonly Key<Item> _uiInspectItem = new();

	/// <summary>Sim-projected seat view; written from sim observations (sim reactive deps), read by UI render.</summary>
	private readonly Key<SeatTileModel> _uiSimBoard = new();
	private readonly Key<(int Cur, int Max)> _uiSimHp = new();
	private readonly Key<Dictionary<Item, int>> _uiSimInv = new();
	private readonly Key<Perk[]> _uiSimPerks = new();
	private readonly Key<CommandDefinition[]> _uiSimCmds = new();
	private readonly Key<bool> _uiSimIdle = new();
	private ProcedureHandle _uiDefaultsProc;
	private ProcedureHandle _uiRenderProc;

	/// <summary>
	/// Keyboard directional command: a command reference that is attempted on WASD with a <see cref="Direction"/> argument.
	/// Must have exactly one <see cref="Var"/> parameter carrying <see cref="ParameterPredicates.Direction"/>.
	/// </summary>
	public CommandDefinition KeyboardCommand { get; set; } = Commands.Move;

	private sealed partial class SeatInputNode : Node
	{
		public LegacyGodotSeatDriver Seat { get; set; } = null!;

		public override void _UnhandledInput(InputEvent @event)
		{
			var seat = Seat;
			if (seat == null)
				return;

			if (@event is InputEventMouseButton mbLeft && mbLeft.Pressed && mbLeft.ButtonIndex == MouseButton.Left)
			{
				if (seat.TryInspectWorld(mbLeft.Position))
					GetViewport()?.SetInputAsHandled();
				return;
			}

			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
			{
				seat.TryShowWorldContextMenu(mb.Position, mb.GlobalPosition);
				return;
			}

			if (@event is not InputEventKey k)
				return;
			if (!k.Pressed || k.Echo)
				return;

			Direction? dir = k.Keycode switch
			{
				Key.W => Direction.Up,
				Key.D => Direction.Right,
				Key.S => Direction.Down,
				Key.A => Direction.Left,
				_ => null
			};
			if (dir == null)
				return;

			if (seat.TrySubmitKeyboardDirection(dir.Value))
				GetViewport()?.SetInputAsHandled();
		}
	}

	private sealed record SeatTileModel(
		Dictionary<Tile, TileCoord> TileToCoord,
		Dictionary<Tile, IOccupant> TileToOccupant);

	private sealed partial class SeatBoard3D : Node3D
	{
		private sealed class TileReferenceEqualityComparer : IEqualityComparer<Tile>
		{
			public static readonly TileReferenceEqualityComparer Instance = new();

			public bool Equals(Tile x, Tile y) => ReferenceEquals(x, y);

			public int GetHashCode(Tile obj) =>
				obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
		}

		private readonly Dictionary<ulong, Tile> _pickNodeToTile = new();
		private readonly Dictionary<Tile, MeshInstance3D> _tileToMesh = new(TileReferenceEqualityComparer.Instance);
		private Tile _highlightedTile;
		private Material _matHighlight;

		public float TileSize { get; set; } = 1.0f;
		public float TileHeight { get; set; } = 0.2f;
		public float TileGap { get; set; } = 0.05f;
		public float MarkerHeight { get; set; } = 0.6f;
		public float BodyScale { get; set; } = 0.85f;
		public float CameraYawDeg { get; set; } = 45.0f;
		public float CameraPitchDeg { get; set; } = 55.0f;
		public float CameraFovDeg { get; set; } = 50.0f;
		public float CameraFitMargin { get; set; } = 1.15f;

		private readonly Dictionary<(int Group, int Row, int Col), MeshInstance3D> _tiles = new();
		private readonly Dictionary<Body, Node3D> _bodies = new();
		private Camera3D _camera;
		private Node3D _root;
		private PackedScene _qbodyScene;

		private Material _matUnknown;
		private Material _matGroup0;
		private Material _matGroup1;
		private Material _matOtherGroup;
		private Material _matStorage;

		public override void _Ready()
		{
			_root = new Node3D { Name = "SeatTiles" };
			AddChild(_root);

			_qbodyScene = ResourceLoader.Load<PackedScene>("res://Assets/qbody.glb");

			_camera = new Camera3D
			{
				Name = "SeatCamera",
				Projection = Camera3D.ProjectionType.Perspective,
				Fov = CameraFovDeg,
				Current = true
			};
			AddChild(_camera);

			_matUnknown = MakeMat(new Color(0.15f, 0.15f, 0.15f));
			_matGroup0 = MakeMat(new Color(0.45f, 0.55f, 0.85f));
			_matGroup1 = MakeMat(new Color(0.65f, 0.50f, 0.80f));
			_matOtherGroup = MakeMat(new Color(0.55f, 0.55f, 0.55f));
			_matStorage = MakeMat(new Color(0.95f, 0.30f, 0.30f));
			_matHighlight = MakeHighlightMat();
		}

		public void Render(SeatTileModel model)
		{
			_tileToMesh.Clear();
			foreach (var kv in _tiles)
			{
				kv.Value.Visible = false;
				if (kv.Value != null && GodotObject.IsInstanceValid(kv.Value))
					kv.Value.MaterialOverlay = null;
			}
			foreach (var kv in _bodies)
			{
				if (kv.Value != null && GodotObject.IsInstanceValid(kv.Value))
					kv.Value.Visible = false;
			}

			if (model == null || model.TileToCoord == null || model.TileToCoord.Count == 0)
				return;

			var minR = int.MaxValue;
			var minC = int.MaxValue;
			var maxR = int.MinValue;
			var maxC = int.MinValue;
			foreach (var kv in model.TileToCoord)
			{
				var c = kv.Value;
				if (c.Row < minR) minR = c.Row;
				if (c.Col < minC) minC = c.Col;
				if (c.Row > maxR) maxR = c.Row;
				if (c.Col > maxC) maxC = c.Col;
			}
			if (minR == int.MaxValue)
				return;

			var step = TileSize + TileGap;
			var placed = new HashSet<Body>();

			foreach (var kv in model.TileToCoord)
			{
				var tile = kv.Key;
				var coord = kv.Value;
				if (tile == null)
					continue;

				IOccupant occ = null;
				model.TileToOccupant?.TryGetValue(tile, out occ);
				var key = (tile.Group.Value, coord.Row, coord.Col);

				if (!_tiles.TryGetValue(key, out var mesh) || !GodotObject.IsInstanceValid(mesh))
				{
					mesh = new MeshInstance3D
					{
						Name = $"Tile_{tile.Group.Value}_{coord.Row}_{coord.Col}",
						Mesh = new BoxMesh()
					};
					_root.AddChild(mesh);
					_tiles[key] = mesh;

					// Add collision for ray-picking (right-click context menu).
					// This creates a StaticBody3D child named after the mesh.
					mesh.CreateTrimeshCollision();
				}
				RegisterPickNodes(mesh, tile);

				var isStorage = occ is Storage;
				var baseH = isStorage ? TileHeight + MarkerHeight : TileHeight;
				var size = new Vector3(TileSize, baseH, TileSize);
				(mesh.Mesh as BoxMesh).Size = size;

				var x = (coord.Col - minC) * step;
				var z = (coord.Row - minR) * step;
				mesh.Position = new Vector3(x, baseH * 0.5f, z);
				mesh.MaterialOverride = isStorage ? _matStorage : MaterialFor(tile);
				_tileToMesh[tile] = mesh;
				mesh.Visible = true;

				if (_qbodyScene != null && occ is Body body && placed.Add(body))
				{
					if (!_bodies.TryGetValue(body, out var node) || !GodotObject.IsInstanceValid(node))
					{
						node = _qbodyScene.Instantiate<Node3D>();
						_root.AddChild(node);
						_bodies[body] = node;
					}

					float sx = 0, sz = 0;
					var n = 0;
					foreach (var t in body.OccupiedTiles())
					{
						if (!model.TileToCoord.TryGetValue(t, out var c))
							continue;
						sx += (c.Col - minC) * step;
						sz += (c.Row - minR) * step;
						n++;
					}
					node.Scale = Vector3.One * (BodyScale * TileSize);
					node.Position = new Vector3(sx / n, TileHeight, sz / n);
					node.Visible = true;
				}
			}

			// Re-apply highlight after rerender if possible.
			if (_highlightedTile != null && _tileToMesh.TryGetValue(_highlightedTile, out var highlightedMesh) &&
				highlightedMesh != null && GodotObject.IsInstanceValid(highlightedMesh))
			{
				highlightedMesh.MaterialOverlay = _matHighlight;
			}

			FrameCamera();
		}

		public void SetHighlightedTile(Tile tile)
		{
			if (ReferenceEquals(tile, _highlightedTile))
				return;

			if (_highlightedTile != null && _tileToMesh.TryGetValue(_highlightedTile, out var prev) &&
				prev != null && GodotObject.IsInstanceValid(prev))
			{
				prev.MaterialOverlay = null;
			}

			_highlightedTile = tile;
			if (_highlightedTile != null && _tileToMesh.TryGetValue(_highlightedTile, out var next) &&
				next != null && GodotObject.IsInstanceValid(next))
			{
				next.MaterialOverlay = _matHighlight;
			}
		}

		public bool TryPickTile(Vector2 screenPos, out Tile tile)
		{
			tile = null;
			if (_camera == null || !GodotObject.IsInstanceValid(_camera))
				return false;

			var world = GetWorld3D();
			if (world == null)
				return false;
			var state = world.DirectSpaceState;
			if (state == null)
				return false;

			var origin = _camera.ProjectRayOrigin(screenPos);
			var dir = _camera.ProjectRayNormal(screenPos);
			var to = origin + dir * 10_000f;

			var query = PhysicsRayQueryParameters3D.Create(origin, to);
			query.CollideWithAreas = true;
			query.CollideWithBodies = true;

			var hit = state.IntersectRay(query);
			if (hit == null || hit.Count == 0)
				return false;

			if (!hit.TryGetValue("collider", out var colliderObj))
				return false;

			var node = colliderObj.AsGodotObject() as Node;
			while (node != null && GodotObject.IsInstanceValid(node))
			{
				if (_pickNodeToTile.TryGetValue(node.GetInstanceId(), out tile) && tile != null)
					return true;
				node = node.GetParent();
			}

			return false;
		}

		private void RegisterPickNodes(Node root, Tile tile)
		{
			if (root == null || tile == null || !GodotObject.IsInstanceValid(root))
				return;
			_pickNodeToTile[root.GetInstanceId()] = tile;
			foreach (var child in root.GetChildren())
			{
				if (child is Node n && GodotObject.IsInstanceValid(n))
					RegisterPickNodes(n, tile);
			}
		}

		private Material MaterialFor(Tile tile)
		{
			if (tile == null) return _matUnknown;
			if (tile.Group.Value == 0) return _matGroup0;
			if (tile.Group.Value == 1) return _matGroup1;
			return _matOtherGroup;
		}

		private void FrameCamera()
		{
			var minX = float.PositiveInfinity;
			var minZ = float.PositiveInfinity;
			var maxX = float.NegativeInfinity;
			var maxZ = float.NegativeInfinity;

			foreach (var kv in _tiles)
			{
				var m = kv.Value;
				if (m == null || !GodotObject.IsInstanceValid(m) || !m.Visible)
					continue;
				var p = m.Position;
				if (p.X < minX) minX = p.X;
				if (p.Z < minZ) minZ = p.Z;
				if (p.X > maxX) maxX = p.X;
				if (p.Z > maxZ) maxZ = p.Z;
			}

			if (!float.IsFinite(minX) || !float.IsFinite(minZ))
				return;

			var pad = TileSize * 0.75f;
			minX -= pad;
			minZ -= pad;
			maxX += pad;
			maxZ += pad;

			var centroid = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);
			var width = maxX - minX;
			var height = maxZ - minZ;

			var vp = GetViewport();
			var aspect = vp != null && vp.GetVisibleRect().Size.Y > 0
				? (float)(vp.GetVisibleRect().Size.X / vp.GetVisibleRect().Size.Y)
				: 16f / 9f;

			_camera.Fov = CameraFovDeg;

			var halfFov = (CameraFovDeg * (MathF.PI / 180.0f)) * 0.5f;
			var tan = MathF.Tan(MathF.Max(0.0001f, halfFov));

			var distForHeight = (height * 0.5f) / tan;
			var distForWidth = (width * 0.5f) / (tan * MathF.Max(0.0001f, aspect));
			var dist = MathF.Max(distForHeight, distForWidth) * MathF.Max(1.0f, CameraFitMargin);

			var yaw = CameraYawDeg * (MathF.PI / 180.0f);
			var pitch = CameraPitchDeg * (MathF.PI / 180.0f);
			var dir = new Vector3(
				MathF.Cos(pitch) * MathF.Sin(yaw),
				MathF.Sin(pitch),
				MathF.Cos(pitch) * MathF.Cos(yaw));

			_camera.Position = centroid + dir * dist;
			_camera.LookAt(centroid, Vector3.Up);
		}

		private static StandardMaterial3D MakeMat(Color color)
		{
			return new StandardMaterial3D
			{
				AlbedoColor = color,
				Roughness = 0.85f,
				Metallic = 0.05f
			};
		}

		private static StandardMaterial3D MakeHighlightMat()
		{
			return new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				AlbedoColor = new Color(1.0f, 0.95f, 0.15f, 0.35f),
				EmissionEnabled = true,
				Emission = new Color(1.0f, 0.95f, 0.15f),
				// Keep it readable on top of existing tile mats.
				Roughness = 1.0f,
				Metallic = 0.0f
			};
		}
	}

	public LegacyGodotSeatDriver(Node seatRoot)
	{
		_seatRoot = seatRoot ?? throw new ArgumentNullException(nameof(seatRoot));
		_uiRoot = new Control
		{
			Name = "Seat0_DecisionUi",
			// Let clicks fall through to the 3D board / _UnhandledInput except on real controls (buttons, etc.).
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_seatRoot.AddChild(_uiRoot);
		_uiCtx = new ExecutionContext(new State());

		_inputNode = new SeatInputNode { Name = "Seat0_Input", Seat = this };
		_uiRoot.AddChild(_inputNode);

		_board3dRoot = new Node3D { Name = "Seat0_Board3D_Root" };
		_board3d = new SeatBoard3D { Name = "Seat0_Board3D" };
		_board3dRoot.AddChild(_board3d);
		_seatRoot.AddChild(_board3dRoot);

		_ui = new ReactiveEngine(_uiCtx);
		_uiDefaultsProc = _ui.AddProcedure(UiDefaultsProcedure);
		_uiRenderProc = _ui.AddProcedure(UiRenderProcedure);
	}

	public SaveNode<LegacyGodotSeatDriverPersistence.DriverSave> SaveTo(SaveSession session) =>
		new(LegacyGodotSeatDriverPersistence.SaveSchemaId, LegacyGodotSeatDriverPersistence.Encode(this));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();

	/// <summary>Parent for this seat’s decision UI (buttons, prompts, etc.).</summary>
	public Control UiRoot => _uiRoot;

	/// <summary>UI-local context for this seat (keys like selection, render models, etc.).</summary>
	public ExecutionContext UiCtx => _uiCtx;

	public bool HasPendingDecision => _submit != null;

	public IReadOnlyList<CommandDefinition> PendingCommands
	{
		get
		{
			if (!HasPendingDecision)
				return Array.Empty<CommandDefinition>();
			return _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();
		}
	}

	private void UiRunScoped(Action body)
	{
		var (_, writes) = _uiCtx.Record(body);
		_ui.PropagateWriteKeys(writes);
		_ui.RunTillQuiescence();
	}

	public void OnDecisionNeeded(Body vehicle, Func<CommandDefinition, Assignment, bool> submit)
	{
		_submit = submit;

		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiHasPendingDecision, true);
			if (vehicle != null)
				_uiCtx.Write(_uiVehicle, vehicle);
		});
	}

	public bool TrySubmitKeyboardDirection(Direction dir)
	{
		if (!HasPendingDecision)
			return false;

		var cmd = KeyboardCommand ?? Commands.Move;
		if (cmd == null || cmd.Variables.Count != 1)
			return false;

		var f = cmd.Constraint;
		var dirVar = cmd.Variables[0];
		if (!Derivation.Derives(f, ParameterPredicates.Direction[dirVar]))
			return false;

		var cmds = _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();
		if (Array.IndexOf(cmds, cmd) < 0)
			return false;

		var vehicle = _uiCtx.Read(_uiVehicle);
		var idle = _uiCtx.Read(_uiSimIdle);
		var canAct = vehicle != null && idle;
		if (!canAct)
			return false;

		if (!cmd.TryBindVariables(dir, out var assignment))
			return false;
		if (!_submit(cmd, assignment))
			return false;

		ClearPendingDecision();
		return true;
	}

	public IEnumerable<Action<Body>> SimObservations
	{
		get { yield return ObserveSim; }
	}

	private void ObserveSim(Body vehicle)
	{
		if (vehicle == null)
		{
			UiRunScoped(ClearSimProjection);
			return;
		}

		var owned = vehicle.Perks.ReadOwned();
		var ownedPerks = owned is Perk[] a ? a : new List<Perk>(owned).ToArray();
		var d = vehicle.Inventory.ReadAll();
		var inv = d is Dictionary<Item, int> dd ? new Dictionary<Item, int>(dd) : new Dictionary<Item, int>(d);
		var hp = (
			Cur: (int)Math.Round(vehicle.Resources.ReadCur(ResourcesCatalog.Health)),
			Max: (int)Math.Round(vehicle.Resources.ReadMax(ResourcesCatalog.Health)));
		var cmds = Commands.AvailableCommands(vehicle);
		var idle = vehicle.ReadActionState() is IdleActionState;
		var board = BuildSeatTileModel(vehicle, maxTiles: 800);

		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiSimBoard, board);
			_uiCtx.Write(_uiSimHp, hp);
			_uiCtx.Write(_uiSimInv, inv);
			_uiCtx.Write(_uiSimPerks, ownedPerks);
			_uiCtx.Write(_uiSimCmds, cmds);
			_uiCtx.Write(_uiSimIdle, idle);
		});
	}

	private void ClearSimProjection()
	{
		_uiCtx.Write(_uiSimBoard, null);
		_uiCtx.Write(_uiSimHp, (0, 0));
		_uiCtx.Write(_uiSimInv, new Dictionary<Item, int>());
		_uiCtx.Write(_uiSimPerks, Array.Empty<Perk>());
		_uiCtx.Write(_uiSimCmds, Array.Empty<CommandDefinition>());
		_uiCtx.Write(_uiSimIdle, false);
	}

	private static SeatTileModel BuildSeatTileModel(Body vehicle, int maxTiles)
	{
		var tileToCoord = DeriveCoordsFromOccupiedFollowEdges(vehicle, maxTiles);
		var tileToOccupant = new Dictionary<Tile, IOccupant>();
		foreach (var kv in tileToCoord)
		{
			var t = kv.Key;
			if (t == null)
				continue;
			var occ = vehicle.Occupancy.GetAt(t);
			if (occ != null)
				tileToOccupant[t] = occ;
		}
		return new SeatTileModel(tileToCoord, tileToOccupant);
	}

	private static Dictionary<Tile, TileCoord> DeriveCoordsFromOccupiedFollowEdges(Body vehicle, int maxTiles)
	{
		var coords = new Dictionary<Tile, TileCoord>();
		var q = new Queue<Tile>();
		var seeds = vehicle.OccupiedTiles();

		var allowedGroups = new HashSet<GroupId>();
		Tile root = null;
		for (var i = 0; i < seeds.Length; i++)
		{
			var t = seeds[i];
			if (t != null)
			{
				allowedGroups.Add(t.Group);
				if (root == null)
					root = t;
			}
		}
		if (root == null)
			return coords;

		coords[root] = new TileCoord(0, 0);
		q.Enqueue(root);

		while (q.Count > 0 && coords.Count < maxTiles)
		{
			var u = q.Dequeue();
			var uCoord = coords[u];

			for (var d = Direction.Up; d <= Direction.Left; d++)
			{
				var e = u.Edge(d);
				var v = e?.To;
				if (v == null)
					continue;
				if (!allowedGroups.Contains(v.Group))
					continue;

				var want = uCoord.Step(d);

				if (coords.TryGetValue(v, out var have))
				{
					if (!have.Equals(want))
						continue;
					continue;
				}

				coords[v] = want;
				q.Enqueue(v);
				if (coords.Count >= maxTiles)
					break;
			}
		}

		return coords;
	}

	public bool TrySubmitWait(long deltaTicks)
	{
		if (!HasPendingDecision)
			return false;
		if (!Commands.Wait.TryBindVariables(deltaTicks, out var assignment))
			return false;
		return TrySubmit(Commands.Wait, assignment);
	}

	private bool TrySubmit(CommandDefinition cmd, Assignment assignment)
	{
		var submit0 = _submit;
		if (submit0 == null)
			return false;
		if (!submit0(cmd, assignment))
			return false;

		// Important: issuing can immediately re-ping and install a new submit delegate.
		// Only clear the pending decision if we're still on the same delegate.
		if (ReferenceEquals(_submit, submit0))
			ClearPendingDecision();
		return true;
	}

	private void EnsureBoardPanel()
	{
		if (_boardPanel != null && GodotObject.IsInstanceValid(_boardPanel))
			return;
		_boardPanel = new VBoxContainer
		{
			Name = "Seat0_Boards",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_uiRoot.AddChild(_boardPanel);
	}

	private static Label UiLabel(string text) =>
		new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };

	private void EnsureContextMenu()
	{
		if (_contextMenu != null && GodotObject.IsInstanceValid(_contextMenu))
			return;
		_contextMenu = new PopupMenu { Name = "Seat0_ContextMenu" };
		_uiRoot.AddChild(_contextMenu);
		_contextMenuOptions = new List<(CommandDefinition, Assignment)>();
		_contextMenu.IdPressed += OnContextMenuIdPressed;
	}

	private void OnContextMenuIdPressed(long id)
	{
		var i = (int)id;
		if (_contextMenuOptions == null || i < 0 || i >= _contextMenuOptions.Count)
			return;
		var (cmd, variableAssignment) = _contextMenuOptions[i];
		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiSelectedCommand, cmd);
			// Menu entry is exactly one contiguous prefix fragment; restart args (don't merge unrelated keys).
			_uiCtx.Write(_uiPartialAssignment, variableAssignment);

			Item itemFromPick = null;
			foreach (var v in cmd.Variables)
			{
				if (variableAssignment.TryGet(v, out var val) && val is Item it)
				{
					itemFromPick = it;
					break;
				}
			}

			_uiCtx.Write(_uiSelectedItem, itemFromPick);
		});
	}

	private static Var TryGetNextUnboundVariable(CommandDefinition cmd, Body vehicle, Assignment partialAssignment)
	{
		if (cmd?.Variables == null)
			return null;
		var prefix = new Assignment();
		foreach (var v in cmd.Variables)
		{
			if (!partialAssignment.TryGet(v, out var val) || val == null)
				return v;
			var next = v.BindOrCheck(prefix, val);
			if (next == null)
				return v;
			if (vehicle != null && !cmd.Constraint.Extendable(vehicle, next))
				return v;
			prefix = next;
		}
		return null;
	}

	private static Assignment NormalizeOrderedPrefix(CommandDefinition cmd, Body vehicle, Assignment partialAssignment)
	{
		var cut = new Assignment();
		if (cmd?.Variables == null || partialAssignment == null)
			return cut;
		foreach (var v in cmd.Variables)
		{
			if (!partialAssignment.TryGet(v, out var val) || val == null)
				break;
			var next = v.BindOrCheck(cut, val);
			if (next == null)
				break;
			if (vehicle != null && !cmd.Constraint.Extendable(vehicle, next))
				break;
			cut = next;
		}
		return cut;
	}

	/// <summary>True when every <see cref="CommandDefinition.Variables"/> binding matches in <paramref name="a"/> and <paramref name="b"/> (reference equality for values).</summary>
	private static bool SameAssignmentBindings(CommandDefinition cmd, Assignment a, Assignment b)
	{
		if (cmd?.Variables == null)
			return true;
		foreach (var v in cmd.Variables)
		{
			a.TryGet(v, out var av);
			b.TryGet(v, out var bv);
			if ((av == null) != (bv == null))
				return false;
			if (av != null && !ReferenceEquals(av, bv))
				return false;
		}
		return true;
	}

	private static bool TryGetFirstVariableOfType(CommandDefinition cmd, Type valueType, out Var variable)
	{
		variable = default!;
		if (cmd == null)
			return false;
		var f = cmd.Constraint;
		foreach (var v in cmd.Variables)
		{
			if (valueType == typeof(Direction) && Derivation.Derives(f, ParameterPredicates.Direction[v]))
			{
				variable = v;
				return true;
			}
			if (valueType == typeof(Item) && Derivation.Derives(f, ParameterPredicates.Item[v]))
			{
				variable = v;
				return true;
			}
			if (valueType == typeof(Tile) && Derivation.Derives(f, ParameterPredicates.Tile[v]))
			{
				variable = v;
				return true;
			}
			if (valueType == typeof(Storage) && Derivation.Derives(f, ParameterPredicates.Storage[v]))
			{
				variable = v;
				return true;
			}
			if (valueType == typeof(long) && Derivation.Derives(f, ParameterPredicates.Long[v]))
			{
				variable = v;
				return true;
			}
		}

		return false;
	}

	private void MaybeAdvanceActiveCommandFromItemClick(Item item, Body vehicle)
	{
		if (item == null)
			return;

		var cmd = _uiCtx.Read(_uiSelectedCommand);
		if (cmd == null)
			return;

		var idle = _uiCtx.Read(_uiSimIdle);
		var hasPending = _uiCtx.Read(_uiHasPendingDecision);
		if (!hasPending || vehicle == null || !idle)
			return;

		var curRaw = _uiCtx.Read(_uiPartialAssignment) ?? new Assignment();
		var cur = NormalizeOrderedPrefix(cmd, vehicle, curRaw);
		var nextHole = TryGetNextUnboundVariable(cmd, vehicle, cur);
		var cstr = cmd.Constraint;
		if (nextHole is not Var itemVar || !Derivation.Derives(cstr, ParameterPredicates.Item[itemVar]))
			return;

		var next = itemVar.BindOrCheck(cur, item);
		if (next == null || !cmd.IsExtendable(vehicle, next))
			return;

		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiPartialAssignment, next);
			_uiCtx.Write(_uiSelectedItem, item);
		});

		var following = TryGetNextUnboundVariable(cmd, vehicle, next);
		if (following is Var amountVar && Derivation.Derives(cstr, ParameterPredicates.Long[amountVar]))
			TryPromptAmountForActiveCommand(cmd);
		else if (following == null && cmd.Constraint.Accepts(vehicle, next))
			TrySubmitActiveCommandIfComplete(cmd, next);
	}

	private List<(CommandDefinition Cmd, Assignment VariableAssignment)> ComputeStorageXferMenuOptions(Body vehicle, Tile tile)
	{
		var list = new List<(CommandDefinition, Assignment)>();
		if (vehicle == null || tile == null)
			return list;

		if (vehicle.Occupancy.GetAt(tile) is not Storage storage)
			return list;

		if (!TryGetFirstVariableOfType(Transfer.DepositCommand, typeof(Storage), out var storageVar))
			return list;

		var partialTarget = storageVar.BindOrCheck(new Assignment(), storage);
		if (partialTarget == null)
			return list;

		if (Transfer.DepositCommand.IsExtendable(vehicle, partialTarget))
			list.Add((Transfer.DepositCommand, partialTarget));
		if (Transfer.WithdrawCommand.IsExtendable(vehicle, partialTarget))
			list.Add((Transfer.WithdrawCommand, partialTarget));

		return list;
	}

	private bool TryPromptAmountForActiveCommand(CommandDefinition cmd)
	{
		var vehicle = _uiCtx.Read(_uiVehicle);
		var partialAssignment = NormalizeOrderedPrefix(cmd, vehicle, _uiCtx.Read(_uiPartialAssignment) ?? new Assignment());
		var hole = TryGetNextUnboundVariable(cmd, vehicle, partialAssignment);
		var cstr = cmd.Constraint;
		if (hole is not Var amountVar || !Derivation.Derives(cstr, ParameterPredicates.Long[amountVar]))
			return false;

		var dlg = new ConfirmationDialog { Title = $"{cmd?.Name ?? "Transfer"} amount" };
		var spin = new SpinBox
		{
			MinValue = 1,
			MaxValue = long.MaxValue,
			Step = 1,
			Value = 1,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddChild(new Label
		{
			Text = "Amount:",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
		row.AddChild(spin);

		// ConfirmationDialog internals vary by engine version; add UI under the Window root reliably.
		dlg.AddChild(row);
		dlg.OkButtonText = "OK";

		CommandDefinition cmd0 = cmd;

		void Close()
		{
			if (GodotObject.IsInstanceValid(dlg))
				dlg.QueueFree();
		}

		dlg.Confirmed += () =>
		{
			var amt = Math.Max(1L, (long)Math.Round(spin.Value));
			var vehicle = _uiCtx.Read(_uiVehicle);
			var cur = _uiCtx.Read(_uiPartialAssignment) ?? new Assignment();
			var partial = NormalizeOrderedPrefix(cmd0, vehicle, cur);
			var assignment = amountVar.BindOrCheck(partial, amt);
			if (assignment == null)
			{
				Close();
				return;
			}

			TryIssueFromActiveCommand(cmd0, assignment);
			Close();
		};
		dlg.Canceled += Close;

		_uiRoot.AddChild(dlg);
		dlg.PopupCentered();
		return true;
	}

	private void TryIssueFromActiveCommand(CommandDefinition cmd, Assignment variableAssignment)
	{
		if (cmd == null || variableAssignment == null)
			return;
		var vehicle = _uiCtx.Read(_uiVehicle);
		var idle = _uiCtx.Read(_uiSimIdle);
		var hasPending = _uiCtx.Read(_uiHasPendingDecision);
		if (!hasPending || vehicle == null || !idle || !cmd.Constraint.Accepts(vehicle, variableAssignment))
		{
			GD.Print("Command issue failed admissibility.");
			return;
		}

		TrySubmitActiveCommandIfComplete(cmd, variableAssignment);
	}

	private void TrySubmitActiveCommandIfComplete(CommandDefinition cmd, Assignment variableAssignment)
	{
		if (cmd == null || variableAssignment == null)
			return;

		if (!TrySubmit(cmd, variableAssignment))
			GD.Print("Command Issue returned false.");
		// Success path clears via ClearPendingDecision inside TrySubmit.
	}

	private static string AssignmentSummary(CommandDefinition cmd, Assignment variableAssignment)
	{
		if (cmd == null || variableAssignment == null)
			return "";
		var parts = new List<string>();
		foreach (var v in cmd.Variables)
		{
			if (variableAssignment.TryGet(v, out var val) && val != null)
				parts.Add(v.Name);
		}
		return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
	}

	private void ShowContextMenu(Vector2 pos, IReadOnlyList<(CommandDefinition Cmd, Assignment VariableAssignment)> options)
	{
		EnsureContextMenu();
		_contextMenu.Clear();
		_contextMenuOptions.Clear();

		if (options == null || options.Count == 0)
		{
			_contextMenu.AddItem("(no matching commands)", 0);
			_contextMenu.SetItemDisabled(0, true);
		}
		else
		{
			for (var i = 0; i < options.Count; i++)
			{
				var (cmd, variableAssignment) = options[i];
				_contextMenuOptions.Add((cmd, variableAssignment));
				_contextMenu.AddItem($"{cmd?.Name ?? "(null)"}{AssignmentSummary(cmd, variableAssignment)}", i);
			}
		}

		_contextMenu.Position = new Vector2I((int)Math.Round(pos.X), (int)Math.Round(pos.Y));
		_contextMenu.Popup();
	}

	/// Variable order is fixed: consume <paramref name="proposalValues"/> strictly in declaration order —
	/// advance the proposal cursor until each variable receives a token that <see cref="Formula.Extendable(Body, Assignment)"/> accepts with <paramref name="vehicle"/>.
	private static bool TryOrderedVariableProposal(CommandDefinition cmd, Body vehicle, IReadOnlyList<object> proposalValues,
		out Assignment variableAssignment)
	{
		variableAssignment = new Assignment();
		if (cmd == null || vehicle == null || proposalValues == null || proposalValues.Count == 0 || cmd.Variables.Count == 0)
			return false;

		var pi = 0;
		foreach (var v in cmd.Variables)
		{
			while (pi < proposalValues.Count)
			{
				var o = proposalValues[pi];
				if (o == null)
				{
					pi++;
					continue;
				}

				var trial = v.BindOrCheck(variableAssignment, o);
				if (trial == null || !cmd.Constraint.Extendable(vehicle, trial))
				{
					pi++;
					continue;
				}

				variableAssignment = trial;
				pi++;
				break;
			}

			if (!variableAssignment.TryGet(v, out var bound) || bound == null)
				return false;
		}

		return true;
	}

	private List<(CommandDefinition Cmd, Assignment VariableAssignment)> ComputeCommandOptionsFromProposal(
		Body vehicle,
		IReadOnlyList<CommandDefinition> cmds,
		IReadOnlyList<object> proposalValues,
		int maxPerCommand = 8)
	{
		var list = new List<(CommandDefinition, Assignment)>();
		if (vehicle == null || cmds == null || proposalValues == null || proposalValues.Count == 0)
			return list;

		var added = 0;
		foreach (var cmd in cmds)
		{
			if (cmd == null)
				continue;
			if (added >= maxPerCommand)
				break;

			if (!TryOrderedVariableProposal(cmd, vehicle, proposalValues, out var variableAssignment))
				continue;
			if (!cmd.IsExtendable(vehicle, variableAssignment))
				continue;

			list.Add((cmd, variableAssignment));
			added++;
		}

		return list;
	}

	private void ClearBoardRender()
	{
		if (_boardPanel == null || !GodotObject.IsInstanceValid(_boardPanel))
			return;
		foreach (var child in _boardPanel.GetChildren())
			(child as Node)?.QueueFree();
	}

	private void ClearPendingDecision()
	{
		_submit = null!;
		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiHasPendingDecision, false);
			_uiCtx.Write(_uiSelectedCommand, null);
			_uiCtx.Write(_uiSelectedItem, null);
			_uiCtx.Write(_uiPartialAssignment, new Assignment());
		});
	}

	private void TryShowWorldContextMenu(Vector2 viewportPos, Vector2 globalPos)
	{
		if (!HasPendingDecision)
			return;
		if (_board3d == null || !GodotObject.IsInstanceValid(_board3d))
			return;

		var vehicle = _uiCtx.Read(_uiVehicle);
		var idle = _uiCtx.Read(_uiSimIdle);
		if (vehicle == null || !idle)
			return;

		var cmds = _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();
		if (cmds.Length == 0)
			return;

		if (!_board3d.TryPickTile(viewportPos, out var tile) || tile == null)
			return;

		_board3d.SetHighlightedTile(tile);

		var occEarly = vehicle.Occupancy.GetAt(tile);
		if (occEarly is Storage)
		{
			var opts = ComputeStorageXferMenuOptions(vehicle, tile);
			ShowContextMenu(globalPos, opts);
			return;
		}

		var proposal = new List<object> { tile };

		// If the clicked tile is adjacent to a currently occupied tile, also offer the implied direction.
		// This makes tile-adjacent clicks naturally surface direction-filled commands like Move/Punch.
		Direction? inferred = null;
		var ambiguous = false;
		foreach (var occTile in vehicle.Occupancy.Occupies(vehicle))
		{
			if (occTile == null)
				continue;

			Direction? d = null;
			if (occTile.Up != null && occTile.Up.Open && ReferenceEquals(occTile.Up.To, tile))
				d = Direction.Up;
			else if (occTile.Right != null && occTile.Right.Open && ReferenceEquals(occTile.Right.To, tile))
				d = Direction.Right;
			else if (occTile.Down != null && occTile.Down.Open && ReferenceEquals(occTile.Down.To, tile))
				d = Direction.Down;
			else if (occTile.Left != null && occTile.Left.Open && ReferenceEquals(occTile.Left.To, tile))
				d = Direction.Left;

			if (d == null)
				continue;
			if (inferred == null)
				inferred = d.Value;
			else if (inferred.Value != d.Value)
			{
				ambiguous = true;
				break;
			}
		}
		if (inferred != null && !ambiguous)
			proposal.Add(inferred.Value);

		var occ = vehicle.Occupancy.GetAt(tile);
		if (occ is Body b)
			proposal.Add(b);

		var options = ComputeCommandOptionsFromProposal(vehicle, cmds, proposal);
		ShowContextMenu(globalPos, options);
	}

	private bool TryInspectWorld(Vector2 viewportPos)
	{
		if (_board3d == null || !GodotObject.IsInstanceValid(_board3d))
			return false;
		if (!_board3d.TryPickTile(viewportPos, out var tile) || tile == null)
			return false;

		_board3d.SetHighlightedTile(tile);
		UiRunScoped(() =>
		{
			_uiCtx.Write(_uiInspectTile, tile);
			_uiCtx.Write(_uiInspectItem, null);
		});
		return true;
	}

	private void UiDefaultsProcedure()
	{
		var hasPending = _uiCtx.Read(_uiHasPendingDecision);
		if (!hasPending)
			return;

		var cmds = _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();

		// No command list UI anymore: don't auto-select a command.
		// Selection typically comes from a context-menu fill proposal.
		var sel = _uiCtx.Read(_uiSelectedCommand);
		if (sel != null && Array.IndexOf(cmds, sel) < 0)
			_uiCtx.Write(_uiSelectedCommand, null);
		sel = _uiCtx.Read(_uiSelectedCommand);

		// Deposit/Withdraw: fills come from clicks + amount dialog; skip default item/amount injection.
		if (sel == Transfer.DepositCommand || sel == Transfer.WithdrawCommand)
			return;

		// Default item selection for PickItem pickers (first owned item).
		var invDict = _uiCtx.Read(_uiSimInv) ?? new Dictionary<Item, int>();
		var selItem = _uiCtx.Read(_uiSelectedItem);
		if (selItem != null && (!invDict.TryGetValue(selItem, out var selN) || selN <= 0))
			_uiCtx.Write(_uiSelectedItem, null);
		selItem = _uiCtx.Read(_uiSelectedItem);
		if (selItem == null)
		{
			foreach (var id in Items.All)
			{
				if (invDict.TryGetValue(id, out var n) && n > 0)
				{
					_uiCtx.Write(_uiSelectedItem, id);
					break;
				}
			}
		}

		// Canonicalize to a contiguous ordered prefix only, then optionally default the single next unset parameter.
		var rawArgs = _uiCtx.Read(_uiPartialAssignment) ?? new Assignment();

		var selected = sel;
		if (selected != null)
		{
			var vehicle = _uiCtx.Read(_uiVehicle);
			var canonical = NormalizeOrderedPrefix(selected, vehicle, rawArgs);
			var merged = canonical;
			var needsClean = !SameAssignmentBindings(selected, rawArgs, canonical);

			var hole = TryGetNextUnboundVariable(selected, vehicle, merged);
			var selF = selected.Constraint;
			if (hole is Var itemVar && Derivation.Derives(selF, ParameterPredicates.Item[itemVar]))
			{
				var itemPick = _uiCtx.Read(_uiSelectedItem);
				if (itemPick != null && invDict.TryGetValue(itemPick, out var pickN) && pickN > 0)
				{
					var holeOk = merged.TryGet(itemVar, out var ho) &&
					             ho is Item kept &&
					             invDict.TryGetValue(kept, out var kn) && kn > 0;
					if (!holeOk)
					{
						var withItem = itemVar.BindOrCheck(merged, itemPick);
						if (withItem != null)
							merged = withItem;
					}
				}
			}
			else if (hole is Var longVar && Derivation.Derives(selF, ParameterPredicates.Long[longVar]) &&
			         !merged.TryGet(longVar, out _))
			{
				var withLong = longVar.BindOrCheck(merged, 0L);
				if (withLong != null)
					merged = withLong;
			}

			if (needsClean || !SameAssignmentBindings(selected, rawArgs, merged))
				_uiCtx.Write(_uiPartialAssignment, merged);
		}
	}

	private void RenderStats((int Cur, int Max) hp)
	{
		_boardPanel.AddChild(UiLabel($"Health: {hp.Cur}/{hp.Max}"));
	}

	private void RenderInspect(
		Body vehicle,
		CommandDefinition[] cmds,
		bool canAct)
	{
		_boardPanel.AddChild(UiLabel("Inspect: (left click world, click items)"));

		var inspectItem = _uiCtx.Read(_uiInspectItem);
		if (inspectItem != null)
		{
			_boardPanel.AddChild(UiLabel($"Item: {inspectItem.Name}"));
			return;
		}

		var inspectTile = _uiCtx.Read(_uiInspectTile);
		if (inspectTile == null)
		{
			_boardPanel.AddChild(UiLabel("(none)"));
			return;
		}

		_boardPanel.AddChild(UiLabel($"Tile: {inspectTile}"));
		if (vehicle == null)
			return;

		var occ = vehicle.Occupancy.GetAt(inspectTile);
		if (occ == null)
		{
			_boardPanel.AddChild(UiLabel("Occupant: (none)"));
			return;
		}

		_boardPanel.AddChild(UiLabel($"Occupant: {occ}"));

		if (occ is Storage storage)
		{
			_boardPanel.AddChild(UiLabel("Chest contents:"));
			RenderItemCountsList(counts: storage.Inventory.ReadAll(), vehicle, cmds, canAct, contextTile: inspectTile);
			return;
		}

		if (occ is Body body)
		{
			var hp = (
				Cur: (int)Math.Round(body.Resources.ReadCur(ResourcesCatalog.Health)),
				Max: (int)Math.Round(body.Resources.ReadMax(ResourcesCatalog.Health)));
			_boardPanel.AddChild(UiLabel($"Health: {hp.Cur}/{hp.Max}"));
			_boardPanel.AddChild(UiLabel("Inventory:"));
			RenderItemCountsList(counts: body.Inventory.ReadAll(), vehicle, cmds, canAct, contextTile: null);
			return;
		}
	}

	private void RenderItemCountsList(
		IReadOnlyDictionary<Item, int> counts,
		Body vehicle,
		IReadOnlyList<CommandDefinition> cmds,
		bool canAct,
		Tile contextTile)
	{
		if (counts == null)
			return;

		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_boardPanel.AddChild(box);

		foreach (var item in Items.All)
		{
			counts.TryGetValue(item, out var n);
			var btn = new Button
			{
				Text = $"{item.Name} x{n}",
				Disabled = !canAct || n <= 0
			};
			btn.GuiInput += ev =>
			{
				if (ev is not InputEventMouseButton mb || !mb.Pressed)
					return;

				if (mb.ButtonIndex == MouseButton.Left)
				{
					UiRunScoped(() =>
					{
						_uiCtx.Write(_uiInspectItem, item);
						_uiCtx.Write(_uiInspectTile, contextTile);
					});
					if (canAct)
						MaybeAdvanceActiveCommandFromItemClick(item, vehicle);
					return;
				}

				if (!canAct || mb.ButtonIndex != MouseButton.Right)
					return;

				var proposal = new List<object> { item };
				var options = ComputeCommandOptionsFromProposal(vehicle, cmds, proposal);
				ShowContextMenu(mb.GlobalPosition, options);
			};
			box.AddChild(btn);
		}
	}

	private void RenderInventory(Dictionary<Item, int> inv)
	{
		var vehicle = _uiCtx.Read(_uiVehicle);
		var idle = _uiCtx.Read(_uiSimIdle);
		var hasPending = _uiCtx.Read(_uiHasPendingDecision);
		var canAct = hasPending && vehicle != null && idle;
		var cmds = _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();

		_boardPanel.AddChild(UiLabel("Inventory:"));
		RenderItemCountsList(
			counts: inv,
			vehicle: vehicle,
			cmds: cmds,
			canAct: canAct,
			contextTile: null);
	}

	private void RenderPerks(Perk[] ownedPerks)
	{
		_boardPanel.AddChild(UiLabel("Perks:"));
		foreach (var perk in PerksCatalog.All)
		{
			var owned = Array.IndexOf(ownedPerks, perk) >= 0;
			var affected = Commands.AffectedByPerk(perk);
			var names = affected.Length == 0 ? "(none)" : string.Join(", ", Array.ConvertAll(affected, c => c.Name));
			_boardPanel.AddChild(UiLabel($"{(owned ? "[owned]" : "[not owned]")} {perk.Name} → {names}"));
		}
	}

	private void RenderNoDecision() => _boardPanel.AddChild(UiLabel("(no decision)"));

	private static bool IsComplete(CommandDefinition cmd, Body vehicle, Assignment argValues)
	{
		if (cmd == null || vehicle == null)
			return false;
		foreach (var v in cmd.Variables)
		{
			if (!argValues.TryGet(v, out var val) || val == null)
				return false;
		}
		return cmd.Constraint.Accepts(vehicle, argValues);
	}

	private void RenderIssueButton(bool canAct, CommandDefinition selected, Body vehicle, Assignment argValues)
	{
		if (selected == null)
			return;

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		var complete = argValues != null && IsComplete(selected, vehicle, argValues);
		var canIssue = canAct && (selected.Variables.Count == 0 || complete);
		var issue = new Button { Text = "Issue", Disabled = !canIssue };
		issue.Pressed += () =>
		{
			if (!HasPendingDecision)
				return;
			var cmd = _uiCtx.Read(_uiSelectedCommand);
			var args = _uiCtx.Read(_uiPartialAssignment) ?? new Assignment();
			var v0 = _uiCtx.Read(_uiVehicle);
			if (cmd == null || v0 == null || !IsComplete(cmd, v0, args))
				return;
			TrySubmit(cmd, args);
		};
		row.AddChild(issue);

		if (selected.Variables.Count > 0 && !complete)
			row.AddChild(UiLabel("(fill all args)"));

		_boardPanel.AddChild(row);
	}

	private void RenderCommands(
		bool hasPending,
		bool canAct,
		CommandDefinition[] cmds,
		CommandDefinition selected,
		Dictionary<Item, int> inv,
		Body vehicle,
		Assignment argValues)
	{
		if (!hasPending)
		{
			RenderNoDecision();
			return;
		}

		_boardPanel.AddChild(UiLabel("Commands: (right click world)"));

		if (selected == null)
		{
			_boardPanel.AddChild(UiLabel("(no command selected)"));
			return;
		}

		_boardPanel.AddChild(UiLabel($"Selected: {selected.Name}"));

		// Deprecated input picker removed: show current filled values (if any) but do not render editors here.
		if (selected.Variables.Count > 0)
		{
			_boardPanel.AddChild(UiLabel("Filled:"));
			foreach (var v in selected.Variables)
			{
				argValues.TryGet(v, out var val);
				_boardPanel.AddChild(UiLabel($"{v.Name}: {val?.ToString() ?? "(null)"}"));
			}
		}

		RenderIssueButton(canAct, selected, vehicle, argValues ?? new Assignment());
	}

	private void UiRenderProcedure()
	{
		var hasPending = _uiCtx.Read(_uiHasPendingDecision);
		var selected = _uiCtx.Read(_uiSelectedCommand);
		var vehicle = _uiCtx.Read(_uiVehicle);
		var argValues = _uiCtx.Read(_uiPartialAssignment) ?? new Assignment();

		var cmds = _uiCtx.Read(_uiSimCmds) ?? Array.Empty<CommandDefinition>();
		var idle = _uiCtx.Read(_uiSimIdle);
		var canAct = hasPending && vehicle != null && idle;

		var ownedPerks = _uiCtx.Read(_uiSimPerks) ?? Array.Empty<Perk>();
		var inv = _uiCtx.Read(_uiSimInv) ?? new Dictionary<Item, int>();
		var hp = _uiCtx.Read(_uiSimHp);

		var board = _uiCtx.Read(_uiSimBoard);

		EnsureBoardPanel();
		ClearBoardRender();

		if (_board3d != null && GodotObject.IsInstanceValid(_board3d))
			_board3d.Render(board);

		RenderStats(hp);
		RenderInspect(vehicle, cmds, canAct);
		RenderInventory(inv);
		RenderPerks(ownedPerks);
		RenderCommands(
			hasPending: hasPending,
			canAct: canAct,
			cmds: cmds,
			selected: selected,
			inv: inv,
			vehicle: vehicle,
			argValues: argValues);

		if (board == null || board.TileToCoord == null || board.TileToCoord.Count == 0)
			_boardPanel.AddChild(UiLabel("World: (none)"));
		else
			_boardPanel.AddChild(UiLabel($"World: (rendered in 3D) tiles={board.TileToCoord.Count}"));
	}
}
