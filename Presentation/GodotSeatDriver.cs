using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Human seat: ordered provenance selection, command classify / complete, board lenses.
/// Sim observations write projection keys; UI procedures derive chrome and render.
/// </summary>
public sealed class GodotSeatDriver : IDriver, ISaveable<GodotSeatDriverPersistence.DriverSave>
{
	private readonly Control _uiRoot;
	private readonly ExecutionContext _uiCtx;
	private readonly ReactiveEngine _ui;

	private readonly ContainerView _items;
	private readonly AmountView _amounts;
	private readonly CommandsView _commands;
	private readonly SelectionView _selectionView;
	private readonly BoardView _board;
	private readonly ISelectionInput[] _selectors;
	private readonly List<FloatingPanel> _panels = new();

	private readonly Key<Body> _vehicle = new();
	private readonly Key<List<SelectionEntry>> _selection = new();
	private readonly Key<CommandDefinition> _chosen = new();
	private readonly Key<CommandDefinition[]> _available = new();
	private readonly Key<ISelectionInput> _focus = new();

	private Func<CommandDefinition, Assignment, bool> _submit;

	public GodotSeatDriver(Node seatRoot)
	{
		_uiCtx = new ExecutionContext(new State());
		_ui = new ReactiveEngine(_uiCtx);

		_uiRoot = new Control
		{
			Name = "Seat_Ui",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		seatRoot.AddChild(_uiRoot);

		var s = ISelectionInput.Slot;
		var itemsPanel = OpenPanel("Items", new Vector2(12, 12));
		_items = new ContainerView(
			_uiCtx,
			itemsPanel,
			ParameterPredicates.Item[s] & Consume.ItemInInventoryDomain[s]);
		_items.ItemPressed += item => UiRunScoped(() => OnPick(item, _items));

		var amountPanel = OpenPanel("Amount", new Vector2(12, 220));
		_amounts = new AmountView(_uiCtx, amountPanel);
		_amounts.AmountPressed += n => UiRunScoped(() => OnPick(n, _amounts));

		var cmdPanel = OpenPanel("Commands", new Vector2(220, 12));
		_commands = new CommandsView(_uiCtx, cmdPanel);
		_commands.CommandPressed += cmd => UiRunScoped(() => OnCommandPressed(cmd));

		var selPanel = OpenPanel("Selection", new Vector2(220, 280));
		_selectionView = new SelectionView(_uiCtx, selPanel.Body);
		var clear = new Button { Text = "Clear", Flat = true };
		clear.Pressed += () => UiRunScoped(ClearAll);
		selPanel.Body.AddChild(clear);

		var boardRoot = new Node3D { Name = "Seat_Board" };
		seatRoot.AddChild(boardRoot);
		_board = new BoardView(_uiCtx, boardRoot);
		_board.TilePressed += t => UiRunScoped(() => OnPick(t, _board.TileSelector));
		_board.OccupantPressed += o => UiRunScoped(() => OnPick(o, _board.OccupantSelector));
		_board.DirectionPressed += d => UiRunScoped(() => OnPick(d, _board.DirectionSelector));

		var lensPanel = OpenPanel("Board lens", new Vector2(220, 400));
		AddLensButton(lensPanel, "None", BoardLens.None);
		AddLensButton(lensPanel, "Tiles", BoardLens.Tile);
		AddLensButton(lensPanel, "Occupants", BoardLens.Occupant);
		AddLensButton(lensPanel, "Direction", BoardLens.Direction);

		InstallPanelTray();

		_selectors =
		[
			_items,
			_amounts,
			_board.DirectionSelector,
			_board.TileSelector,
			_board.OccupantSelector
		];

		_ui.AddProcedure(Derive);
		_ui.AddProcedure(_items.Render);
		_ui.AddProcedure(_amounts.Render);
		_ui.AddProcedure(_commands.Render);
		_ui.AddProcedure(_selectionView.Render);
		_ui.AddProcedure(_board.Render);
		_ui.AddProcedure(ApplyChrome);
	}

	public ExecutionContext UiCtx => _uiCtx;

	public IEnumerable<Action<Body>> SimObservations
	{
		get
		{
			yield return ObserveInventory;
			yield return ObserveCommands;
			yield return ObserveBoard;
		}
	}

	public void OnDecisionNeeded(Body vehicle, Func<CommandDefinition, Assignment, bool> submit)
	{
		_submit = submit;
		UiRunScoped(() => _uiCtx.Write(_vehicle, vehicle));
	}

	private void AddLensButton(FloatingPanel panel, string label, BoardLens lens)
	{
		var btn = new Button { Text = label, Flat = true };
		btn.Pressed += () => UiRunScoped(() => _board.SetLens(lens));
		panel.Body.AddChild(btn);
	}

	private FloatingPanel OpenPanel(string title, Vector2 position)
	{
		var panel = new FloatingPanel(title, position);
		_uiRoot.AddChild(panel);
		_panels.Add(panel);
		return panel;
	}

	private void InstallPanelTray()
	{
		var tray = new HBoxContainer
		{
			Name = "PanelTray",
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		tray.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		tray.OffsetTop = -36;
		tray.OffsetBottom = -4;
		tray.OffsetLeft = 8;
		tray.OffsetRight = -8;
		foreach (var panel in _panels)
		{
			var title = panel.Title;
			var btn = new Button { Text = title, Flat = true };
			var captured = panel;
			btn.Pressed += () => captured.ShowPanel();
			tray.AddChild(btn);
		}
		_uiRoot.AddChild(tray);
	}

	private List<SelectionEntry> ReadSelection() =>
		_uiCtx.Read(_selection) ?? new List<SelectionEntry>();

	private void OnPick(object value, ISelectionInput source)
	{
		var chosen = _uiCtx.Read(_chosen);
		if (chosen != null)
		{
			if (source.CandidateFilter != null && !source.CandidateFilter(value))
				return;
			_uiCtx.Write(_selection, SeatSelection.Append(ReadSelection(), value, source.Guarantee));
			TryIssueIfReady();
			return;
		}

		var shift = Input.IsKeyPressed(Key.Shift);
		_uiCtx.Write(_selection, shift
			? SeatSelection.Append(ReadSelection(), value, source.Guarantee)
			: SeatSelection.Replace(value, source.Guarantee));
	}

	private void OnCommandPressed(CommandDefinition cmd)
	{
		var vehicle = _uiCtx.Read(_vehicle);
		var sel = ReadSelection();
		var truth = SeatCommandLogic.Evaluate(cmd, vehicle, sel);
		if (truth == PartialTruth.False)
			return;
		if (truth == PartialTruth.True)
		{
			TryIssue(cmd, SeatCommandLogic.ToAssignment(cmd, sel));
			return;
		}
		_uiCtx.Write(_chosen, cmd);
		_commands.SetChosen(cmd);
	}

	private void TryIssueIfReady()
	{
		var vehicle = _uiCtx.Read(_vehicle);
		var cmd = _uiCtx.Read(_chosen);
		var sel = ReadSelection();
		if (cmd == null || vehicle == null)
			return;
		if (SeatCommandLogic.Evaluate(cmd, vehicle, sel) != PartialTruth.True)
			return;
		TryIssue(cmd, SeatCommandLogic.ToAssignment(cmd, sel));
	}

	private void TryIssue(CommandDefinition cmd, Assignment assignment)
	{
		if (_submit == null)
			return;
		var submit0 = _submit;
		if (!submit0(cmd, assignment))
			return;
		if (ReferenceEquals(_submit, submit0))
			_submit = null;
		ClearAll();
	}

	private void ClearAll()
	{
		_uiCtx.Write(_selection, new List<SelectionEntry>());
		_uiCtx.Write(_chosen, null);
		_commands.SetChosen(null);
	}

	/// <summary>Derive command rows, completion focus/filters, selection readout, highlights.</summary>
	private void Derive()
	{
		var vehicle = _uiCtx.Read(_vehicle);
		var sel = ReadSelection();
		var chosen = _uiCtx.Read(_chosen);
		var available = _uiCtx.Read(_available) ?? Array.Empty<CommandDefinition>();

		foreach (var s in _selectors)
			s.ClearPrompt();

		_selectionView.SetEntries(sel);
		SyncHighlights(sel);
		WriteCommandRows(vehicle, available, sel);

		ISelectionInput focus = null;
		if (chosen != null && vehicle != null
		    && SeatCommandLogic.Evaluate(chosen, vehicle, sel) == PartialTruth.Unknown)
		{
			var hole = SeatCommandLogic.NextHole(chosen, sel);
			if (hole != null)
			{
				foreach (var s in _selectors)
				{
					if (!SeatCommandLogic.ProvenanceOk(chosen, hole, s.Guarantee))
						continue;
					focus = s;
					break;
				}

				foreach (var s in _selectors)
				{
					if (!ReferenceEquals(s, focus))
					{
						s.CandidateFilter = _ => false;
						continue;
					}

					var partial = sel;
					var guarantee = s.Guarantee;
					s.CandidateFilter = value =>
					{
						var trial = SeatSelection.Append(partial, value, guarantee);
						return SeatCommandLogic.Evaluate(chosen, vehicle, trial) != PartialTruth.False;
					};
					s.Prompt(hole);
				}
			}
		}

		_uiCtx.Write(_focus, focus);
	}

	private void ApplyChrome()
	{
		var focus = _uiCtx.Read(_focus);
		var boardPick = focus != null && focus.IsBoardLens;
		foreach (var panel in _panels)
			panel.SetPassThrough(boardPick);
		focus?.Panel?.FocusOpen();
	}

	private void WriteCommandRows(Body vehicle, CommandDefinition[] available, List<SelectionEntry> sel)
	{
		if (vehicle == null)
		{
			_commands.SetRows(Array.Empty<CommandsView.Row>());
			return;
		}

		var rows = new List<CommandsView.Row>();
		foreach (var cmd in available)
		{
			var t = SeatCommandLogic.Evaluate(cmd, vehicle, sel);
			if (t == PartialTruth.False)
				continue;
			rows.Add(new CommandsView.Row(cmd, t));
		}
		_commands.SetRows(rows.ToArray());
	}

	private void SyncHighlights(List<SelectionEntry> sel)
	{
		var items = new HashSet<Item>();
		long? amount = null;
		var boardHl = new HashSet<object>();
		foreach (var e in sel)
		{
			if (ReferenceEquals(e.Guarantee, _items.Guarantee) && e.Value is Item it)
				items.Add(it);
			if (ReferenceEquals(e.Guarantee, _amounts.Guarantee) && e.Value is long n)
				amount = n;
			if (ReferenceEquals(e.Guarantee, _board.TileSelector.Guarantee) && e.Value is Tile t)
				boardHl.Add(t);
			if (ReferenceEquals(e.Guarantee, _board.OccupantSelector.Guarantee) && e.Value is IOccupant o)
				boardHl.Add(o);
			if (ReferenceEquals(e.Guarantee, _board.DirectionSelector.Guarantee) && e.Value is Direction d)
				boardHl.Add(d);
		}
		_items.SetHighlighted(items);
		_amounts.SetHighlighted(amount);
		_board.SetHighlighted(boardHl);
	}

	private void UiRunScoped(Action body)
	{
		var (_, writes) = _uiCtx.Record(body);
		_ui.PropagateWriteKeys(writes);
		_ui.RunTillQuiescence();
	}

	private void ObserveInventory(Body vehicle)
	{
		var inv = new Dictionary<Item, int>();
		if (vehicle != null)
		{
			foreach (var kv in vehicle.Inventory.ReadAll())
			{
				if (kv.Value > 0)
					inv[kv.Key] = kv.Value;
			}
		}

		UiRunScoped(() =>
		{
			if (vehicle != null)
				_uiCtx.Write(_vehicle, vehicle);
			_items.SetCounts(inv);
			PruneMissingItems(inv);
		});
	}

	private void PruneMissingItems(Dictionary<Item, int> inv)
	{
		var sel = ReadSelection();
		var next = new List<SelectionEntry>(sel.Count);
		var changed = false;
		foreach (var e in sel)
		{
			if (ReferenceEquals(e.Guarantee, _items.Guarantee) && e.Value is Item it
			    && (!inv.TryGetValue(it, out var n) || n <= 0))
			{
				changed = true;
				continue;
			}
			next.Add(e);
		}
		if (changed)
			_uiCtx.Write(_selection, next);
	}

	private void ObserveCommands(Body vehicle)
	{
		var cmds = vehicle == null
			? Array.Empty<CommandDefinition>()
			: Commands.AvailableCommands(vehicle);
		UiRunScoped(() =>
		{
			if (vehicle != null)
				_uiCtx.Write(_vehicle, vehicle);
			_uiCtx.Write(_available, cmds);
		});
	}

	private void ObserveBoard(Body vehicle)
	{
		BoardModel model;
		if (vehicle == null)
		{
			model = new BoardModel(
				new Dictionary<Tile, TileCoord>(),
				new Dictionary<Tile, IOccupant>(),
				Vector3.Zero);
		}
		else
		{
			var tiles = LayoutTiles(vehicle, 800);
			var occ = new Dictionary<Tile, IOccupant>();
			foreach (var t in tiles.Keys)
			{
				var o = vehicle.Occupancy.GetAt(t);
				if (o != null)
					occ[t] = o;
			}
			model = new BoardModel(tiles, occ, FootingOnPlane(vehicle, tiles));
		}

		UiRunScoped(() => _board.SetModel(model));
	}

	/// <summary>Centroid of the vehicle's occupied tiles on y = 0 (matches <see cref="BoardView"/> layout).</summary>
	private Vector3 FootingOnPlane(Body vehicle, Dictionary<Tile, TileCoord> tiles)
	{
		var step = _board.TileSize + _board.TileGap;
		var sx = 0f;
		var sz = 0f;
		var n = 0;
		foreach (var t in vehicle.OccupiedTiles())
		{
			if (t == null || !tiles.TryGetValue(t, out var c))
				continue;
			sx += c.Col * step;
			sz += c.Row * step;
			n++;
		}
		if (n == 0)
			return Vector3.Zero;
		return new Vector3(sx / n, 0f, sz / n);
	}

	private static Dictionary<Tile, TileCoord> LayoutTiles(Body vehicle, int maxTiles)
	{
		var coords = new Dictionary<Tile, TileCoord>();
		var seeds = vehicle.OccupiedTiles();
		var allowed = new HashSet<GroupId>();
		Tile root = null;
		foreach (var t in seeds)
		{
			if (t == null)
				continue;
			allowed.Add(t.Group);
			root ??= t;
		}
		if (root == null)
			return coords;

		var q = new Queue<Tile>();
		coords[root] = new TileCoord(0, 0);
		q.Enqueue(root);
		while (q.Count > 0 && coords.Count < maxTiles)
		{
			var u = q.Dequeue();
			var uCoord = coords[u];
			for (var d = Direction.Up; d <= Direction.Left; d++)
			{
				var v = u.Edge(d)?.To;
				if (v == null || !allowed.Contains(v.Group) || coords.ContainsKey(v))
					continue;
				coords[v] = uCoord.Step(d);
				q.Enqueue(v);
			}
		}
		return coords;
	}

	public SaveNode<GodotSeatDriverPersistence.DriverSave> SaveTo(SaveSession session) =>
		new(GodotSeatDriverPersistence.SaveSchemaId, GodotSeatDriverPersistence.Encode(this));

	SaveNode ISaveable.SaveTo(SaveSession session) => SaveTo(session).Untyped();
}
