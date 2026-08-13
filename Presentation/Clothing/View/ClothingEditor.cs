using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DelaunyFabric.Core;
using Godot;

namespace DelaunyFabric.View;

public enum ClothingEditorStep
{
	Author,
	Simulate,
	Finish,
}

/// <summary>
/// Clothing pipeline editor (animation-editor style):
/// 1 Author panels/sews/placements → 2 simulate topology → 3 coarsen/bake maps/mesh.
/// </summary>
public partial class ClothingEditor : Node3D
{
	public GarmentPattern Pattern
	{
		get => _session.Pattern;
		private set => _session.Pattern = value ?? GarmentPattern.CreateDefaultSquare();
	}

	readonly PatternSession _session = new() { Pattern = GarmentPattern.CreateDefaultSquare() };
	public Topology SimTopology { get; private set; }
	public ClothingEditorStep Step { get; private set; } = ClothingEditorStep.Author;

	PatternCanvas _canvas;
	PatternBodyMarkers _markers;
	MeshInstance3D _clothSurface;
	MeshInstance3D _clothGraph;
	Label _status;
	SpinBox _subdivBox;
	SpinBox _relaxBox;
	SpinBox _skinBox;
	SpinBox _frictionBox;
	SpinBox _gravityBox;
	SpinBox _uvScaleBox;
	SpinBox _coarseErrBox;
	FileDialog _savePatternDialog;
	FileDialog _loadPatternDialog;
	FileDialog _loadTopoDialog;
	bool _syncing;

	CancellationTokenSource _solverCancel;
	Task _solverTask;
	Topology _workerTopology;
	MeshTriangleCollider _collider;
	readonly object _snapshotLock = new();
	SolverSnapshot _latestSnapshot;
	int _simBatch;
	const float GravityDecay = 0.99f;

	static readonly string DefaultPatternPath = "res://ik/garment.tres";
	static readonly string DefaultTopoPath = "user://garment.topo";
	static readonly string DefaultObjPath = "user://garment_coarse.obj";
	static readonly string DefaultNormalPath = "user://garment_normal.png";
	static readonly string DefaultCurvaturePath = "user://garment_curvature.png";

	Node3D _bodyRoot;
	Camera3D _camera;

	public void Setup(Node3D bodyRoot, Camera3D camera)
	{
		_bodyRoot = bodyRoot;
		_camera = camera;
		EnsureBodyCollision(bodyRoot);

		_clothSurface = new MeshInstance3D
		{
			Name = "ClothSurface",
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.55f, 0.7f, 0.95f, 0.85f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			},
		};
		AddChild(_clothSurface);
		_clothGraph = new MeshInstance3D { Name = "ClothGraph" };
		AddChild(_clothGraph);

		_markers = new PatternBodyMarkers
		{
			Name = "PatternBodyMarkers",
			Session = _session,
			Camera = camera,
		};
		AddChild(_markers);

		BuildUi();
		_session.SelectionChanged += () =>
		{
			_canvas.QueueRedraw();
			_markers.OnNodeSelectionChanged();
			SetStatus(_session.SelectedIsland >= 0
				? $"Island[{_session.SelectedIsland}]"
				: _session.Selected >= 0 ? $"Node[{_session.Selected}]" : "Nothing selected.");
		};
		_session.PatternChanged += () =>
		{
			if (Step != ClothingEditorStep.Author)
				return;
			RefreshAuthoring();
		};
		RefreshAuthoring();
		if (ResourceLoader.Exists(DefaultPatternPath))
		{
			try
			{
				Pattern = GarmentPattern.Load(DefaultPatternPath);
				RefreshAuthoring();
				SetStatus($"Loaded {DefaultPatternPath}");
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		}
	}

	void BuildUi()
	{
		var layer = new CanvasLayer { Name = "ClothingUi", Layer = 2 };
		AddChild(layer);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -500;
		panel.OffsetTop = 8;
		panel.OffsetRight = -8;
		panel.OffsetBottom = -8;
		panel.AnchorBottom = 1f;
		layer.AddChild(panel);

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		panel.AddChild(scroll);

		var vbox = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		scroll.AddChild(vbox);
		vbox.AddChild(new Label { Text = "Clothing" });

		var steps = new HBoxContainer();
		vbox.AddChild(steps);
		AddButton(steps, "1 Author", () => SetStep(ClothingEditorStep.Author));
		AddButton(steps, "2 Simulate", () => SetStep(ClothingEditorStep.Simulate));
		AddButton(steps, "3 Finish", () => SetStep(ClothingEditorStep.Finish));

		vbox.AddChild(new Label { Text = "Pattern (UV) — box select, Shift+click, Ctrl+C/V" });
		_canvas = new PatternCanvas { Session = _session };
		var canvasFrame = new AspectRatioContainer
		{
			Ratio = 1f,
			StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
			CustomMinimumSize = new Vector2(460, 460),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		canvasFrame.AddChild(_canvas);
		vbox.AddChild(canvasFrame);

		var modes = new HBoxContainer();
		vbox.AddChild(modes);
		AddButton(modes, "Select", () => _session.Mode = PatternEditMode.Select);
		AddButton(modes, "Add", () => _session.Mode = PatternEditMode.AddNode);
		AddButton(modes, "Connect", () => _session.Mode = PatternEditMode.Connect);
		AddButton(modes, "Sew", () => _session.Mode = PatternEditMode.Sew);

		var gizmoRow = new HBoxContainer();
		vbox.AddChild(gizmoRow);
		AddButton(gizmoRow, "Move", () =>
		{
			_markers.SetGizmoMode(IkTransformGizmo.GizmoMode.Translate);
			SetStatus("Gizmo: translate island or node offset (LMB drag axes)");
		});
		AddButton(gizmoRow, "Rotate", () =>
		{
			_markers.SetGizmoMode(IkTransformGizmo.GizmoMode.Rotate);
			SetStatus("Gizmo: rotate island or node offset (LMB drag rings)");
		});

		var snapRow = new HBoxContainer();
		vbox.AddChild(snapRow);
		var snapCheck = new CheckBox { Text = "Snap UV", ButtonPressed = false };
		snapCheck.Toggled += on =>
		{
			_session.SnapEnabled = on;
			_canvas.QueueRedraw();
		};
		snapRow.AddChild(snapCheck);
		var snapDiv = new SpinBox
		{
			MinValue = 2,
			MaxValue = 64,
			Step = 1,
			Value = 16,
			Prefix = "1/",
		};
		snapDiv.ValueChanged += v =>
		{
			_session.SnapDivisions = (int)v;
			_canvas.QueueRedraw();
		};
		snapRow.AddChild(snapDiv);

		_uvScaleBox = Spin("uv scale", 0.5, 0.01, 5, 0.01);
		_uvScaleBox.ValueChanged += v =>
		{
			if (_syncing || Pattern == null) return;
			Pattern.UvScale = (float)v;
			RefreshAuthoring();
		};
		vbox.AddChild(_uvScaleBox);

		var clipRow = new HBoxContainer();
		vbox.AddChild(clipRow);
		AddButton(clipRow, "Copy", () =>
		{
			_session.CopySelection();
			SetStatus(_session.HasClipboard ? $"Copied {_session.Selection.Count} node(s)." : "Nothing selected.");
		});
		AddButton(clipRow, "Paste", () =>
		{
			if (!_session.HasClipboard) { SetStatus("Clipboard empty."); return; }
			_session.PasteClipboard();
			SetStatus("Pasted.");
		});
		AddButton(clipRow, "Paste ↔X", () =>
		{
			if (!_session.HasClipboard) { SetStatus("Clipboard empty."); return; }
			_session.PasteClipboard(mirrorX: true);
			SetStatus("Pasted mirrored X.");
		});
		AddButton(clipRow, "Paste ↕Y", () =>
		{
			if (!_session.HasClipboard) { SetStatus("Clipboard empty."); return; }
			_session.PasteClipboard(mirrorY: true);
			SetStatus("Pasted mirrored Y.");
		});

		var editRow = new HBoxContainer();
		vbox.AddChild(editRow);
		AddButton(editRow, "Delete", () =>
		{
			_session.DeleteSelection();
			RefreshAuthoring();
			SetStatus("Deleted selection.");
		});
		AddButton(editRow, "Zero offset", () =>
		{
			_session.ZeroSelectionOffset();
			SetStatus(_session.Selection.Count > 0 ? "Zeroed node offset(s)." : "No nodes selected.");
		});
		AddButton(editRow, "Reset square", () =>
		{
			Pattern = GarmentPattern.CreateDefaultSquare();
			RefreshAuthoring();
			SetStatus("Reset to default square panel.");
		});

		var io = new HBoxContainer();
		vbox.AddChild(io);
		AddButton(io, "Save pattern", () =>
		{
			_savePatternDialog.CurrentPath = DefaultPatternPath;
			_savePatternDialog.PopupCentered();
		});
		AddButton(io, "Load pattern", () =>
		{
			_loadPatternDialog.CurrentPath = DefaultPatternPath;
			_loadPatternDialog.PopupCentered();
		});

		vbox.AddChild(new Label { Text = "Simulate" });
		_subdivBox = Spin("subdiv uv", 0.01, 0.001, 1, 0.01);
		vbox.AddChild(_subdivBox);
		_relaxBox = Spin("relax batch", 200, 1, 2000, 1);
		vbox.AddChild(_relaxBox);
		_skinBox = Spin("skin", 0.015, 0.0, 0.2, 0.001);
		vbox.AddChild(_skinBox);
		_frictionBox = Spin("friction", 5.0, 0.0, 50, 0.1);
		vbox.AddChild(_frictionBox);
		_gravityBox = Spin("gravity", 0.005, 0.0, 0.05, 0.0005);
		vbox.AddChild(_gravityBox);
		var simIo = new HBoxContainer();
		vbox.AddChild(simIo);
		AddButton(simIo, "Play", OnPlay);
		AddButton(simIo, "Pause", OnPause);
		AddButton(simIo, "Save", OnSaveTopo);
		AddButton(simIo, "Load", () =>
		{
			_loadTopoDialog.CurrentPath = DefaultTopoPath;
			_loadTopoDialog.PopupCentered();
		});

		vbox.AddChild(new Label { Text = "Finish" });
		_coarseErrBox = Spin("coarse err", 0.0004, 0.00001, 0.01, 0.0001);
		vbox.AddChild(_coarseErrBox);
		AddButton(vbox, "Coarsen + bake", OnFinish);

		_status = new Label { Text = "Author: draw panels, sew, place on body (LMB drag markers)." };
		_status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(_status);

		_savePatternDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.SaveFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; Garment Pattern"],
		};
		_savePatternDialog.FileSelected += path =>
		{
			try
			{
				GarmentPattern.Save(Pattern, path);
				SetStatus($"Saved pattern {path}");
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		};
		layer.AddChild(_savePatternDialog);

		_loadPatternDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; Garment Pattern"],
		};
		_loadPatternDialog.FileSelected += path =>
		{
			try
			{
				Pattern = GarmentPattern.Load(path);
				SetStep(ClothingEditorStep.Author);
				RefreshAuthoring();
				SetStatus($"Loaded pattern {path}");
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		};
		layer.AddChild(_loadPatternDialog);

		_loadTopoDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Userdata,
			Filters = ["*.topo ; Relaxed topology"],
		};
		_loadTopoDialog.FileSelected += path =>
		{
			try
			{
				ShowRelaxedTopology(TopologyFile.Load(ProjectSettings.GlobalizePath(path)), path);
			}
			catch (Exception ex)
			{
				SetStatus(ex.Message);
			}
		};
		layer.AddChild(_loadTopoDialog);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Step != ClothingEditorStep.Simulate)
			return;
		if (!ApplyLatestSnapshot())
			return;
		UpdateSimMeshes();
		SetStatus($"Playing… batch {_simBatch}  verts {SimTopology?.Vertices.Count ?? 0}");
	}

	public override void _ExitTree()
	{
		StopSolver();
	}

	void SetStep(ClothingEditorStep step)
	{
		if (step == ClothingEditorStep.Author)
		{
			StopSolver();
			SimTopology = null;
			_workerTopology = null;
		}
		Step = step;
		_markers.Visible = step == ClothingEditorStep.Author;
		if (step == ClothingEditorStep.Author)
			TryPreviewAuthorMesh();
		SetStatus(step switch
		{
			ClothingEditorStep.Author => "Author: UV graph; 3D Move/Rotate gizmo per island.",
			ClothingEditorStep.Simulate => "Simulate: Play / Pause / Save.",
			_ => "Finish: coarsen/resolve/bake from last sim.",
		});
	}

	void RefreshAuthoring()
	{
		if (!_markers.GizmoBusy)
			_markers.Rebuild();
		_canvas.QueueRedraw();
		SyncUvScaleBox();
		TryPreviewAuthorMesh();
	}

	void SyncUvScaleBox()
	{
		if (_uvScaleBox == null || Pattern == null)
			return;
		_syncing = true;
		_uvScaleBox.Value = Pattern.UvScale > 1e-8f ? Pattern.UvScale : 0.5;
		_syncing = false;
	}

	void TryPreviewAuthorMesh()
	{
		try
		{
			var topo = GarmentPatternBuild.BuildTopology(Pattern, sew: false);
			_clothSurface.Mesh = TopologyMeshBuilder.Build(topo);
			_clothGraph.Mesh = null;
		}
		catch (Exception ex)
		{
			SetStatus($"Preview: {ex.Message}");
		}
	}

	bool SolverRunning => _solverCancel != null && !_solverCancel.IsCancellationRequested;

	void OnPlay()
	{
		try
		{
			if (SolverRunning)
				return;

			if (SimTopology == null)
			{
				var topo = GarmentPatternBuild.BuildTopology(Pattern);
				float subdiv = (float)_subdivBox.Value;
				if (subdiv > 0f)
					topo = TopologySubdivision.Subdivide(topo, subdiv);
				SimTopology = topo;
				_workerTopology = TopologyClone.Clone(topo);
				_simBatch = 0;
			}
			else if (_workerTopology == null)
			{
				_workerTopology = TopologyClone.Clone(SimTopology);
			}

			_collider = GodotMeshCollider.BuildFrom(_bodyRoot);
			_markers.Visible = false;
			Step = ClothingEditorStep.Simulate;
			UpdateSimMeshes();
			StartSolver();
			SetStatus($"Playing… {SimTopology.Vertices.Count} verts");
		}
		catch (Exception ex)
		{
			SetStatus($"Play failed: {ex.Message}");
			GD.PushError(ex.ToString());
		}
	}

	void OnPause()
	{
		if (!SolverRunning && SimTopology == null)
		{
			SetStatus("Nothing to pause.");
			return;
		}

		StopSolver();
		UpdateSimMeshes();
		SetStatus($"Paused at batch {_simBatch}");
	}

	void OnSaveTopo()
	{
		try
		{
			ApplyLatestSnapshot();
			if (SimTopology == null)
			{
				SetStatus("Nothing to save — Play first.");
				return;
			}

			var path = ProjectSettings.GlobalizePath(DefaultTopoPath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			TopologyFile.Save(SimTopology, path);
			SetStatus($"Saved {DefaultTopoPath}");
		}
		catch (Exception ex)
		{
			SetStatus($"Save failed: {ex.Message}");
		}
	}

	void StartSolver()
	{
		if (_workerTopology == null || _collider == null)
			return;

		_solverCancel = new CancellationTokenSource();
		var token = _solverCancel.Token;
		var topology = _workerTopology;
		var collider = _collider;
		float skin = (float)_skinBox.Value;
		float friction = (float)_frictionBox.Value;
		float gravity = (float)_gravityBox.Value;
		float uvScale = Pattern.UvScale;
		int batch = Mathf.Max(1, (int)_relaxBox.Value);

		_solverTask = Task.Run(() => RunSolver(topology, collider, skin, friction, gravity, uvScale, batch, token), token);
	}

	void RunSolver(
		Topology topology,
		MeshTriangleCollider collider,
		float skin,
		float friction,
		float gravityAmount,
		float uvScale,
		int batch,
		CancellationToken token)
	{
		var gravity = new Vector3(0f, -gravityAmount, 0f);
		int iteration = _simBatch;
		while (!token.IsCancellationRequested)
		{
			TopologyRelaxation.Relax(
				topology,
				collider,
				skin,
				friction,
				0f,
				gravity,
				uvScale,
				batch);
			PublishSnapshot(topology, ++iteration);
			gravity *= Mathf.Clamp(GravityDecay, 0f, 1f);
			Thread.Sleep(1);
		}
	}

	void PublishSnapshot(Topology topology, int batch)
	{
		var positions = new Vector3[topology.Vertices.Count];
		var contactNormals = new Vector3[topology.Vertices.Count];
		var hasContact = new bool[topology.Vertices.Count];
		for (int i = 0; i < positions.Length; i++)
		{
			positions[i] = topology.Vertices[i].Xyz;
			if (topology.Vertices[i].ContactNormal is Vector3 n)
			{
				contactNormals[i] = n;
				hasContact[i] = true;
			}
		}

		lock (_snapshotLock)
			_latestSnapshot = new SolverSnapshot(positions, contactNormals, hasContact, batch);
	}

	bool ApplyLatestSnapshot()
	{
		if (SimTopology == null)
			return false;

		SolverSnapshot snapshot;
		lock (_snapshotLock)
		{
			snapshot = _latestSnapshot;
			_latestSnapshot = null;
		}

		if (snapshot == null || snapshot.Positions.Length != SimTopology.Vertices.Count)
			return false;

		for (int i = 0; i < snapshot.Positions.Length; i++)
		{
			SimTopology.Vertices[i].Xyz = snapshot.Positions[i];
			SimTopology.Vertices[i].ContactNormal = snapshot.HasContact[i]
				? snapshot.ContactNormals[i]
				: null;
		}

		_simBatch = snapshot.Batch;
		return true;
	}

	void StopSolver()
	{
		_solverCancel?.Cancel();
		try
		{
			_solverTask?.Wait();
		}
		catch (AggregateException)
		{
		}

		_solverCancel?.Dispose();
		_solverCancel = null;
		_solverTask = null;
		ApplyLatestSnapshot();
	}

	void UpdateSimMeshes()
	{
		if (SimTopology == null)
			return;
		_clothSurface.Mesh = TopologyMeshBuilder.Build(SimTopology);
		_clothGraph.Mesh = TopologyMeshBuilder.BuildDebugMarkers(SimTopology, 0.003f, Pattern.UvScale);
	}

	void ShowRelaxedTopology(Topology topo, string source)
	{
		StopSolver();
		SimTopology = topo;
		_workerTopology = TopologyClone.Clone(topo);
		_simBatch = 0;
		_markers.Visible = false;
		Step = ClothingEditorStep.Simulate;
		UpdateSimMeshes();
		SetStatus($"Loaded {topo.Vertices.Count} verts from {source}");
	}

	void OnFinish()
	{
		try
		{
			StopSolver();
			ApplyLatestSnapshot();
			Topology source = SimTopology;
			if (source == null)
			{
				var existing = ProjectSettings.GlobalizePath(DefaultTopoPath);
				if (!File.Exists(existing))
				{
					SetStatus("No sim topology — Play first.");
					return;
				}
				source = TopologyFile.Load(existing);
				SimTopology = source;
			}

			var collider = GodotMeshCollider.BuildFrom(_bodyRoot);
			var coarse = TopologyCoarsening.Coarsen(
				TopologyClone.Clone(source),
				(float)_coarseErrBox.Value);
			TopologyFaceSdfResolve.Resolve(coarse, collider, 20, 0.002f);

			var normalPath = ProjectSettings.GlobalizePath(DefaultNormalPath);
			TopologyNormalBake.BakeAndSave(coarse, source, 1, 1, normalPath);

			var curvPath = ProjectSettings.GlobalizePath(DefaultCurvaturePath);
			TopologyCurvatureBake.BakeAndSave(coarse, 1, 1, curvPath, 0f);

			var objPath = ProjectSettings.GlobalizePath(DefaultObjPath);
			TopologyMeshBuilder.SaveObj(coarse, objPath);

			_clothSurface.Mesh = TopologyMeshBuilder.Build(coarse);
			_clothGraph.Mesh = TopologyMeshBuilder.BuildDebugMarkers(coarse, 0.003f, Pattern.UvScale);
			_markers.Visible = false;
			Step = ClothingEditorStep.Finish;
			SetStatus($"Coarsened → {DefaultObjPath}");
		}
		catch (Exception ex)
		{
			SetStatus($"Finish failed: {ex.Message}");
			GD.PushError(ex.ToString());
		}
	}

	void SetStatus(string text) => _status.Text = text ?? "";

	static SpinBox Spin(string prefix, double value, double min, double max, double step)
	{
		return new SpinBox
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			Value = value,
			Prefix = prefix + " ",
		};
	}

	static Button AddButton(Control parent, string text, Action onPressed)
	{
		var b = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		b.Pressed += onPressed;
		parent.AddChild(b);
		return b;
	}

	static void EnsureBodyCollision(Node3D bodyRoot)
	{
		foreach (var child in bodyRoot.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (child is not MeshInstance3D mi || mi.Mesh == null)
				continue;
			if (mi.FindChild("ClothingEditorCollision", false) != null)
				continue;

			var body = new StaticBody3D { Name = "ClothingEditorCollision" };
			var shape = new CollisionShape3D();
			var faces = mi.Mesh.GetFaces();
			if (faces == null || faces.Length < 3)
				continue;
			shape.Shape = mi.Mesh.CreateTrimeshShape();
			body.AddChild(shape);
			mi.AddChild(body);
		}
	}

	sealed class SolverSnapshot
	{
		public SolverSnapshot(Vector3[] positions, Vector3[] contactNormals, bool[] hasContact, int batch)
		{
			Positions = positions;
			ContactNormals = contactNormals;
			HasContact = hasContact;
			Batch = batch;
		}

		public Vector3[] Positions { get; }
		public Vector3[] ContactNormals { get; }
		public bool[] HasContact { get; }
		public int Batch { get; }
	}
}
