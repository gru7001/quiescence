using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Godot;

public partial class Main : Node
{
	private Game _game = null!;
	private Scheduler _sched = null!;
	private DecisionObligations _obligations = null!;
	private GodotSeatDriver _seat = null!;
	private FooDriver _fooDriver = null!;
	private Body _body = null!;
	private Body _body2 = null!;
	private CancellationTokenSource _simCts = null!;
	private System.Threading.Tasks.Task _simTask = null!;
	private Node _sessionRoot = null!;

	public override void _Ready()
	{
		ResetSessionRoot();
		BuildNewGame();
		InstallSaveLoadUi();
		StartSim();
	}

	public override void _ExitTree()
	{
		DisposeRuntime();
	}

	private void DisposeRuntime()
	{
		_simCts?.Cancel();
		_simCts?.Dispose();
		_simTask = null!;

		_seat = null!;

		if (_sessionRoot != null && GodotObject.IsInstanceValid(_sessionRoot))
			_sessionRoot.QueueFree();
		_sessionRoot = null!;
	}

	private void ResetSessionRoot()
	{
		if (_sessionRoot != null && GodotObject.IsInstanceValid(_sessionRoot))
			_sessionRoot.QueueFree();
		_sessionRoot = new Node { Name = "SessionRoot" };
		AddChild(_sessionRoot);
	}

	private void InstallSaveLoadUi()
	{
		var packed = GD.Load<PackedScene>("res://UI/SaveLoadUi.tscn");
		var ui = packed.Instantiate<Control>();
		AddChild(ui);
	}

	internal void SaveToDefaultPath()
	{
		var save = GameFile.Save(_game);
		var json = JsonSerializer.Serialize(save, SaveJson.Options);
		GodotUserFiles.WriteAllText("user://save.json", json);
		GD.Print($"Saved to {ProjectSettings.GlobalizePath("user://save.json")}");
	}

	internal void LoadFromDefaultPath()
	{
		var json = GodotUserFiles.ReadAllText("user://save.json");
		var file = JsonSerializer.Deserialize<SaveFile>(json, SaveJson.Options);
		if (file == null)
			throw new InvalidOperationException("Failed to deserialize save file.");
		LoadFrom(file);
	}

	private void LoadFrom(SaveFile file)
	{
		DisposeRuntime();
		ResetSessionRoot();

		var ls = new LoadSession(SaveContext.Default, seatRoot: _sessionRoot);
		ls.Index(file);
		var g = (Game)ls.Ref(new NodeRef(file.GameRootId));
		var sched = new Scheduler(ls.Ctx, g.Clock) { RealTimeSpeedTicksPerSecond = 10000 };
		sched.RunScoped(ls.Drain);
		_game = g;
		_sched = sched;
		_obligations = g.Obligations;

		Game.SetupRuntime(_game, _sched);

		// Track the loaded seat driver for future disposal (if present).
		_seat = null!;
		var pairs = _obligations.Ctx.Read(_obligations.RegisteredCouplings);
		if (pairs != null)
		{
			foreach (var ob in pairs)
				if (ob.Driver is GodotSeatDriver seat)
					_seat = seat;
		}

		StartSim();
		GD.Print("Loaded save.");
	}

	private void BuildNewGame()
	{
		var state = new State();
		var ctx = new ExecutionContext(state);
		var clock = new Clock(ctx);
		_sched = new Scheduler(ctx, clock) { RealTimeSpeedTicksPerSecond = 10000 };
		var occ = new Occupancy(ctx);
		var world = new World(ctx);
		_body = new Body(ctx, occ);
		_body2 = new Body(ctx, occ);

		var chest = new Storage(ctx);
		_fooDriver = new FooDriver();

		// Build two rectangular display groups of tiles with fully open internal adjacency.
		var g0 = (group: new GroupId(0), width: 8, height: 6);
		var g1 = (group: new GroupId(1), width: 6, height: 6);
		var tiles = BuildRectGroups(g0, g1);

		// Tie two edges together directly (no Connector type).
		var aEdge = RectTile(tiles, g0, row: 2, col: 7).Right;
		var bEdge = RectTile(tiles, g1, row: 2, col: 0).Left;
		aEdge.SetTo(bEdge.From);
		bEdge.SetTo(aEdge.From);
		aEdge.SetOpen(true);
		bEdge.SetOpen(true);
		_obligations = new DecisionObligations(ctx);
		_seat = new GodotSeatDriver(_sessionRoot);
		_game = new Game(clock, occ, world, _obligations, bodies: new() { _body, _body2 });
		_sched.RunScoped(() =>
		{
			_game.RegisterCoupling(_seat, _body);
			_game.RegisterCoupling(_fooDriver, _body2);
			world.WriteTiles(tiles);

			if (!occ.TryAdd(_body, RectTile(tiles, g0, row: 2, col: 2)))
				throw new InvalidOperationException("Failed to place body on initial board tile.");
			if (!occ.TryAdd(chest, RectTile(tiles, g0, row: 2, col: 3)))
				throw new InvalidOperationException("Failed to place chest on initial board tile.");
			if (!occ.TryAdd(_body2, RectTile(tiles, g1, row: 2, col: 2)))
				throw new InvalidOperationException("Failed to place second body on group-1 board.");
			_body.WriteActionState(IdleActionState.Instance);
			_body.Stats.Write(StatsCatalog.MoveSpeed, 1.0f);
			_body.Resources.WriteMax(ResourcesCatalog.Health, 20.0f);
			_body.Resources.WriteCur(ResourcesCatalog.Health, 10.0f);
			_body.Inventory.Add(Items.Potion, 2);
			_body.Inventory.Add(Items.Bread, 1);
			chest.Inventory.Add(Items.Bread, 2);
			chest.Inventory.Add(Items.Potion, 1);
			_body2.WriteActionState(IdleActionState.Instance);
			_body2.Stats.Write(StatsCatalog.MoveSpeed, 1.0f);
			_body2.Resources.WriteMax(ResourcesCatalog.Health, 20.0f);
			_body2.Resources.WriteCur(ResourcesCatalog.Health, 20.0f);
		});

		Game.SetupRuntime(_game, _sched);
	}

	private static List<Tile> BuildRectGroups(params (GroupId group, int width, int height)[] groups)
	{
		// Flatten into one tile array with stable integer ids.
		var total = 0;
		for (var gi = 0; gi < groups.Length; gi++)
			total += groups[gi].width * groups[gi].height;

		var tiles = new List<Tile>(total);
		for (var i = 0; i < total; i++)
			tiles.Add(null);

		var offset = 0;
		for (var gi = 0; gi < groups.Length; gi++)
		{
			var (g, w, h) = groups[gi];
			for (var r = 0; r < h; r++)
			for (var c = 0; c < w; c++)
			{
				var id = offset + (r * w + c);
				tiles[id] = new Tile(g);
			}
			offset += w * h;
		}

		// Wire edges after all tiles exist.
		offset = 0;
		for (var gi = 0; gi < groups.Length; gi++)
		{
			var (g, w, h) = groups[gi];
			for (var r = 0; r < h; r++)
			for (var c = 0; c < w; c++)
			{
				var id = offset + (r * w + c);
				var tile = tiles[id];

				var upTo = r > 0 ? tiles[id - w] : null;
				var rightTo = c < w - 1 ? tiles[id + 1] : null;
				var downTo = r < h - 1 ? tiles[id + w] : null;
				var leftTo = c > 0 ? tiles[id - 1] : null;

				tile.SetEdges(
					up: new Edge(tile, Direction.Up, upTo, open: upTo != null),
					right: new Edge(tile, Direction.Right, rightTo, open: rightTo != null),
					down: new Edge(tile, Direction.Down, downTo, open: downTo != null),
					left: new Edge(tile, Direction.Left, leftTo, open: leftTo != null));
			}
			offset += w * h;
		}

		return tiles;
	}

	private static Tile RectTile(List<Tile> tiles, (GroupId group, int width, int height) group, int row, int col)
	{
		if ((uint)row >= (uint)group.height || (uint)col >= (uint)group.width)
			return null;

		var offset = group.group.Value == 0 ? 0 : (8 * 6); // demo-specific: group0 then group1.
		return tiles[offset + row * group.width + col];
	}

	private void StartSim()
	{
		_simCts = new CancellationTokenSource();
		_sched.RunTillQuiescence();
		_simTask = DecisionSystem.StepRealTime(_sched, _obligations, _simCts.Token);
	}

	private static void ClearChildren(Node n)
	{
		foreach (var c in n.GetChildren())
			c.QueueFree();
	}
}
