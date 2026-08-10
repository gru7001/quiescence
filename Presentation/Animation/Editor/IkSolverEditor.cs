using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Edit an IkTargetSet (targets v1): list UI, 3D markers, save/load .tres.
/// Selected target: editable origin + right / up / look (−Z).
/// </summary>
public partial class IkSolverEditor : Node3D
{
	public IkTargetSet Data { get; private set; } = new();

	public event Action ResetToRestPressed;

	Skeleton3D skeleton;
	Node3D markerRoot;
	CanvasLayer uiLayer;
	ItemList list;
	OptionButton bonePick;
	SpinBox weightBox;
	Label status;
	FileDialog saveDialog;
	FileDialog loadDialog;
	bool syncingUi;
	bool uiBuilt;

	SpinBox[] originBox = new SpinBox[3];
	SpinBox[] rightBox = new SpinBox[3];
	SpinBox[] upBox = new SpinBox[3];
	SpinBox[] lookBox = new SpinBox[3];
	IkTransformGizmo gizmo;
	bool gizmoWasBusy;

	readonly List<Node3D> markers = [];

	static readonly string DefaultPath = "res://ik/solver.tres";

	public void Setup(Skeleton3D sk, IkTargetSet initial = null)
	{
		skeleton = sk;
		Data = initial ?? new IkTargetSet();
		BuildMarkers();
		if (!uiBuilt)
		{
			BuildUi();
			gizmo = new IkTransformGizmo { Name = "TransformGizmo" };
			AddChild(gizmo);
			uiBuilt = true;
		}
		RefreshList();
		if (Data.Targets.Count > 0)
		{
			list.Select(0);
			BindGizmoToSelected();
			LoadSpinboxesFromSelected();
		}
	}

	/// <summary>Swap the edited solver data (flushes markers into previous Data first).</summary>
	public void ReplaceData(IkTargetSet data)
	{
		if (weightBox != null)
			PullFromMarkers();
		Data = data ?? new IkTargetSet();
		if (weightBox != null)
			weightBox.Value = Data.TargetWeight;
		BuildMarkers();
		RefreshList();
		if (Data.Targets.Count > 0)
		{
			list.Select(0);
			BindGizmoToSelected();
			LoadSpinboxesFromSelected();
		}
		else if (gizmo != null)
			gizmo.Bound = null;
	}

	/// <summary>Show/hide markers, gizmo, and UI panel (CanvasLayer).</summary>
	public void SetEditingVisible(bool visible)
	{
		Visible = visible;
		if (uiLayer != null)
			uiLayer.Visible = visible;
		if (gizmo != null && !visible)
			gizmo.Bound = null;
	}

	public void PullFromMarkers()
	{
		if (weightBox != null)
			Data.TargetWeight = (float)weightBox.Value;
		for (int i = 0; i < markers.Count && i < Data.Targets.Count; i++)
		{
			IkTargetEntry e = Data.Targets[i];
			if (e == null) continue;
			e.Transform = markers[i].GlobalTransform;
		}
	}

	public TransformIkTerm BuildTransformTerm()
	{
		PullFromMarkers();
		return Data.BuildTransformTerm();
	}

	void BuildMarkers()
	{
		markerRoot?.QueueFree();
		markers.Clear();
		markerRoot = new Node3D { Name = "TargetMarkers" };
		AddChild(markerRoot);

		var mat = new StandardMaterial3D
		{
			AlbedoColor = Colors.Magenta,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
		};
		var mesh = new BoxMesh { Size = new Vector3(0.025f, 0.025f, 0.05f) };

		foreach (IkTargetEntry entry in Data.Targets)
		{
			if (entry == null) continue;
			var marker = new Node3D { Name = string.IsNullOrEmpty(entry.Bone) ? "target" : entry.Bone };
			markerRoot.AddChild(marker);
			marker.GlobalTransform = entry.Transform;
			marker.AddChild(new MeshInstance3D
			{
				Mesh = mesh,
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			});
			AddAxisRays(marker);
			markers.Add(marker);
		}
	}

	static void AddAxisRays(Node3D marker)
	{
		const float Len = 0.12f;
		marker.AddChild(MakeRay("Right", Vector3.Right * Len, Colors.Red));
		marker.AddChild(MakeRay("Up", Vector3.Up * Len, Colors.Green));
		marker.AddChild(MakeRay("Look", Vector3.Forward * Len, Colors.Cyan)); // local −Z = look
	}

	static MeshInstance3D MakeRay(string name, Vector3 to, Color color)
	{
		var mi = new MeshInstance3D { Name = name };
		var im = new ImmediateMesh();
		mi.Mesh = im;
		mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		mi.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true,
		};
		im.SurfaceBegin(Mesh.PrimitiveType.Lines);
		im.SurfaceAddVertex(Vector3.Zero);
		im.SurfaceAddVertex(to);
		im.SurfaceEnd();
		return mi;
	}

	void BuildUi()
	{
		uiLayer = new CanvasLayer { Name = "IkSolverUi" };
		AddChild(uiLayer);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		panel.OffsetLeft = 12;
		panel.OffsetTop = 12;
		panel.OffsetRight = 360;
		panel.OffsetBottom = 620;
		uiLayer.AddChild(panel);

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(330, 590),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		panel.AddChild(scroll);

		var vbox = new VBoxContainer();
		scroll.AddChild(vbox);

		vbox.AddChild(new Label { Text = "IK Solver (targets)" });

		weightBox = new SpinBox
		{
			MinValue = 0,
			MaxValue = 100,
			Step = 0.05,
			Value = Data.TargetWeight,
			Prefix = "w ",
		};
		vbox.AddChild(weightBox);

		list = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
		list.ItemSelected += OnItemSelected;
		vbox.AddChild(list);

		bonePick = new OptionButton();
		FillBonePick();
		vbox.AddChild(bonePick);

		var row = new HBoxContainer();
		vbox.AddChild(row);
		AddButton(row, "Add", OnAdd);
		AddButton(row, "Remove", OnRemove);
		AddButton(row, "Snap rest", OnSnapRest);

		var row2 = new HBoxContainer();
		vbox.AddChild(row2);
		AddButton(row2, "Save", () => { saveDialog.CurrentPath = DefaultPath; saveDialog.PopupCentered(); });
		AddButton(row2, "Load", () => { loadDialog.CurrentPath = DefaultPath; loadDialog.PopupCentered(); });
		AddButton(row2, "Reset rest", () =>
		{
			ResetToRestPressed?.Invoke();
			status.Text = "Skeleton reset to rest";
		});

		var row3 = new HBoxContainer();
		vbox.AddChild(row3);
		AddButton(row3, "Move", () =>
		{
			gizmo.SetMode(IkTransformGizmo.GizmoMode.Translate);
			status.Text = "Gizmo: translate (LMB drag axes)";
		});
		AddButton(row3, "Rotate", () =>
		{
			gizmo.SetMode(IkTransformGizmo.GizmoMode.Rotate);
			status.Text = "Gizmo: rotate (LMB drag rings)";
		});

		vbox.AddChild(new HSeparator());
		vbox.AddChild(new Label { Text = "Selected transform" });
		vbox.AddChild(new Label { Text = "Origin" });
		AddVec3Row(vbox, originBox, OnSpinboxEdited);
		vbox.AddChild(new Label { Text = "Right (red)" });
		AddVec3Row(vbox, rightBox, OnSpinboxEdited);
		vbox.AddChild(new Label { Text = "Up (green)" });
		AddVec3Row(vbox, upBox, OnSpinboxEdited);
		vbox.AddChild(new Label { Text = "Look −Z (cyan)" });
		AddVec3Row(vbox, lookBox, OnSpinboxEdited);

		status = new Label { Text = "Select a target; edit origin / axes below." };
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(status);

		saveDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.SaveFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; IK Solver"],
		};
		saveDialog.FileSelected += path =>
		{
			try
			{
				PullFromMarkers();
				IkTargetSet.Save(Data, path);
				status.Text = $"Saved {path} ({Data.Targets.Count} targets)";
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		uiLayer.AddChild(saveDialog);

		loadDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; IK Solver"],
		};
		loadDialog.FileSelected += path =>
		{
			try
			{
				Data = IkTargetSet.Load(path);
				weightBox.Value = Data.TargetWeight;
				BuildMarkers();
				RefreshList();
				if (Data.Targets.Count > 0)
				{
					list.Select(0);
					BindGizmoToSelected();
					LoadSpinboxesFromSelected();
				}
				else
					gizmo.Bound = null;
				status.Text = $"Loaded {path} ({Data.Targets.Count} targets)";
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		uiLayer.AddChild(loadDialog);
	}

	static void AddButton(HBoxContainer row, string text, Action pressed)
	{
		var btn = new Button { Text = text };
		btn.Pressed += pressed;
		row.AddChild(btn);
	}

	static void AddVec3Row(VBoxContainer parent, SpinBox[] boxes, Action edited)
	{
		var row = new HBoxContainer();
		parent.AddChild(row);
		string[] prefixes = ["X", "Y", "Z"];
		for (int i = 0; i < 3; i++)
		{
			boxes[i] = new SpinBox
			{
				MinValue = -100,
				MaxValue = 100,
				Step = 0.01,
				CustomMinimumSize = new Vector2(100, 0),
				Prefix = prefixes[i] + " ",
				AllowLesser = true,
				AllowGreater = true,
			};
			boxes[i].ValueChanged += _ => edited();
			row.AddChild(boxes[i]);
		}
	}

	void FillBonePick()
	{
		bonePick.Clear();
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
			bonePick.AddItem(skeleton.GetBoneName(i), i);
	}

	void RefreshList()
	{
		syncingUi = true;
		list.Clear();
		for (int i = 0; i < Data.Targets.Count; i++)
		{
			IkTargetEntry e = Data.Targets[i];
			list.AddItem($"{i}: {e?.Bone ?? "?"}");
		}
		syncingUi = false;
	}

	int SelectedIndex()
	{
		var sel = list.GetSelectedItems();
		return sel.Length == 0 ? -1 : sel[0];
	}

	void OnItemSelected(long index)
	{
		if (syncingUi || index < 0 || index >= markers.Count) return;
		status.Text = $"Selected {Data.Targets[(int)index].Bone}";
		BindGizmoToSelected();
		LoadSpinboxesFromSelected();
	}

	void BindGizmoToSelected()
	{
		int i = SelectedIndex();
		gizmo.Bound = i >= 0 && i < markers.Count ? markers[i] : null;
	}

	void LoadSpinboxesFromSelected()
	{
		int i = SelectedIndex();
		if (i < 0 || i >= markers.Count) return;

		Transform3D t = markers[i].GlobalTransform;
		Vector3 origin = t.Origin;
		Vector3 right = t.Basis.X;
		Vector3 up = t.Basis.Y;
		Vector3 look = -t.Basis.Z;

		syncingUi = true;
		SetVec3(originBox, origin);
		SetVec3(rightBox, right);
		SetVec3(upBox, up);
		SetVec3(lookBox, look);
		syncingUi = false;
	}

	static void SetVec3(SpinBox[] boxes, Vector3 v)
	{
		boxes[0].Value = v.X;
		boxes[1].Value = v.Y;
		boxes[2].Value = v.Z;
	}

	static Vector3 GetVec3(SpinBox[] boxes) =>
		new((float)boxes[0].Value, (float)boxes[1].Value, (float)boxes[2].Value);

	void OnSpinboxEdited()
	{
		if (syncingUi) return;
		int i = SelectedIndex();
		if (i < 0 || i >= markers.Count) return;

		Vector3 origin = GetVec3(originBox);
		Vector3 right = GetVec3(rightBox);
		Vector3 up = GetVec3(upBox);
		Vector3 look = GetVec3(lookBox);

		if (right.LengthSquared() < 1e-10f || up.LengthSquared() < 1e-10f || look.LengthSquared() < 1e-10f)
			return;

		// Basis columns: X=right, Y=up, Z=−look
		var basis = new Basis(right.Normalized(), up.Normalized(), -look.Normalized()).Orthonormalized();
		var xf = new Transform3D(basis, origin);
		markers[i].GlobalTransform = xf;
		Data.Targets[i].Transform = xf;

		// Refresh spinboxes to orthonormalized values
		syncingUi = true;
		SetVec3(rightBox, basis.X);
		SetVec3(upBox, basis.Y);
		SetVec3(lookBox, -basis.Z);
		syncingUi = false;
	}

	void OnAdd()
	{
		if (bonePick.Selected < 0) return;
		string bone = bonePick.GetItemText(bonePick.Selected);
		int bi = skeleton.FindBone(bone);
		if (bi < 0)
			throw new InvalidOperationException($"bone '{bone}' not found");

		PullFromMarkers();
		Data.AddTarget(bone, skeleton.GetBoneGlobalRest(bi));
		BuildMarkers();
		RefreshList();
		list.Select(Data.Targets.Count - 1);
		BindGizmoToSelected();
		LoadSpinboxesFromSelected();
		status.Text = $"Added {bone}";
	}

	void OnRemove()
	{
		int i = SelectedIndex();
		if (i < 0) return;
		PullFromMarkers();
		Data.RemoveAt(i);
		BuildMarkers();
		RefreshList();
		if (Data.Targets.Count > 0)
		{
			list.Select(Math.Min(i, Data.Targets.Count - 1));
			BindGizmoToSelected();
			LoadSpinboxesFromSelected();
		}
		else
			gizmo.Bound = null;
		status.Text = "Removed target";
	}

	void OnSnapRest()
	{
		int i = SelectedIndex();
		if (i < 0) return;
		IkTargetEntry e = Data.Targets[i];
		int bi = skeleton.FindBone(e.Bone);
		if (bi < 0)
			throw new InvalidOperationException($"bone '{e.Bone}' not found");
		e.Transform = skeleton.GetBoneGlobalRest(bi);
		markers[i].GlobalTransform = e.Transform;
		LoadSpinboxesFromSelected();
		status.Text = $"Snapped {e.Bone} to rest";
	}

	public override void _Process(double delta)
	{
		PullFromMarkers();

		bool busy = gizmo != null && gizmo.Busy;
		if (gizmoWasBusy && !busy)
			LoadSpinboxesFromSelected();
		else if (busy)
			LoadSpinboxesFromSelected();
		gizmoWasBusy = busy;
	}
}
