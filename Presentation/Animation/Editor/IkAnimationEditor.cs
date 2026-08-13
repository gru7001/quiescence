using Godot;
using System;

/// <summary>
/// Edit an IkAnimation: keyframe list, per-key IkSolverEditor, bake + play.
/// </summary>
public partial class IkAnimationEditor : Node3D
{
	public IkAnimation Data { get; private set; } = new();
	public bool Playing { get; private set; }
	public bool HasActiveKey => selectedIndex >= 0 && selectedIndex < Data.Keys.Count;

	public event Action ResetToRestPressed;

	Rig rig;
	IkTermStack terms;
	IkTrackAnimation baked;
	IkSolverEditor solverEditor;
	float playTime;
	int selectedIndex = -1;
	bool syncingUi;
	bool solverSetup;
	IkTargetSet poseClipboard;

	ItemList keyList;
	SpinBox timeBox;
	SpinBox durationBox;
	CheckBox cyclicCheck;
	SpinBox bakeStepsBox;
	Label status;
	FileDialog saveDialog;
	FileDialog loadDialog;
	FileDialog saveTrackDialog;
	Button playButton;

	static readonly string DefaultPath = "res://ik/animation.tres";
	static readonly string DefaultTrackPath = QbodyIk.WalkTrackPath;

	public void Setup(
		Rig bodyRig,
		IkTermStack bodyTerms,
		IkAnimation initial = null)
	{
		rig = bodyRig;
		terms = bodyTerms;
		Data = initial ?? new IkAnimation();

		solverEditor = new IkSolverEditor { Name = "IkSolverEditor" };
		AddChild(solverEditor);
		solverEditor.ResetToRestPressed += () => ResetToRestPressed?.Invoke();

		BuildUi();

		if (Data.Keys.Count == 0)
		{
			var seed = new IkTargetSet();
			SeedRestTarget(seed, "toe.L");
			SeedRestTarget(seed, "toe.R");
			Data.AddKey(0f, seed);
		}

		SelectKey(0);
	}

	void FlushActiveKey()
	{
		if (!HasActiveKey) return;
		solverEditor.PullFromMarkers();
		IkAnimKey key = GetKey(selectedIndex);
		key.TargetSet = solverEditor.Data;
		key.Time = (float)timeBox.Value;
	}

	public TransformIkTerm BuildActiveTransformTerm()
	{
		return solverEditor.BuildTransformTerm();
	}

	public float ActiveTargetWeight => solverEditor.Data.TargetWeight;

	/// <summary>Play baked key poses with Catmull–Rom between them.</summary>
	public void TickPlay(Skeleton3D sk, float delta)
	{
		if (!Playing) return;
		playTime += delta;
		baked.PlayAt(sk, playTime);
	}

	void PullClipSettingsFromUi()
	{
		if (durationBox != null)
			Data.Duration = (float)durationBox.Value;
		if (cyclicCheck != null)
			Data.Cyclic = cyclicCheck.ButtonPressed;
	}

	void PushClipSettingsToUi()
	{
		if (durationBox == null) return;
		syncingUi = true;
		durationBox.Value = Data.EffectiveDuration();
		cyclicCheck.ButtonPressed = Data.Cyclic;
		syncingUi = false;
	}

	void SeedRestTarget(IkTargetSet data, string bone)
	{
		int i = rig.FindBone(bone);
		if (i < 0)
			throw new InvalidOperationException($"SeedRestTarget: bone '{bone}' not found");
		data.AddTarget(bone, rig.GetBoneGlobalRest(i));
	}

	IkAnimKey GetKey(int index) => Data.Keys[index];

	void BuildUi()
	{
		var layer = new CanvasLayer { Name = "IkAnimUi", Layer = 2 };
		AddChild(layer);

		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -320;
		panel.OffsetTop = 12;
		panel.OffsetRight = -12;
		panel.OffsetBottom = 420;
		layer.AddChild(panel);

		var vbox = new VBoxContainer();
		panel.AddChild(vbox);

		vbox.AddChild(new Label { Text = "IK Animation" });

		keyList = new ItemList { CustomMinimumSize = new Vector2(0, 160) };
		keyList.ItemSelected += OnKeySelected;
		vbox.AddChild(keyList);

		timeBox = new SpinBox
		{
			MinValue = 0,
			MaxValue = 600,
			Step = 0.05,
			Value = 0,
			Prefix = "t ",
			Suffix = "s",
		};
		timeBox.ValueChanged += OnTimeChanged;
		vbox.AddChild(timeBox);

		durationBox = new SpinBox
		{
			MinValue = 0.05,
			MaxValue = 600,
			Step = 0.05,
			Value = Data.EffectiveDuration(),
			Prefix = "len ",
			Suffix = "s",
		};
		durationBox.ValueChanged += v =>
		{
			if (syncingUi) return;
			Data.Duration = (float)v;
		};
		vbox.AddChild(durationBox);

		cyclicCheck = new CheckBox { Text = "Cyclic" };
		cyclicCheck.ButtonPressed = Data.Cyclic;
		cyclicCheck.Toggled += on =>
		{
			if (syncingUi) return;
			Data.Cyclic = on;
		};
		vbox.AddChild(cyclicCheck);

		var row = new HBoxContainer();
		vbox.AddChild(row);
		AddButton(row, "Add", OnAddKey);
		AddButton(row, "Remove", OnRemoveKey);
		AddButton(row, "Duplicate", OnDuplicateKey);

		var rowPose = new HBoxContainer();
		vbox.AddChild(rowPose);
		AddButton(rowPose, "Copy pose", OnCopyPose);
		AddButton(rowPose, "Paste pose", OnPastePose);
		AddButton(rowPose, "Paste mirrored", OnPastePoseMirrored);

		var row2 = new HBoxContainer();
		vbox.AddChild(row2);
		AddButton(row2, "Save", () =>
		{
			saveDialog.CurrentPath = DefaultPath;
			saveDialog.PopupCentered();
		});
		AddButton(row2, "Load", () =>
		{
			loadDialog.CurrentPath = DefaultPath;
			loadDialog.PopupCentered();
		});

		bakeStepsBox = new SpinBox
		{
			MinValue = 1,
			MaxValue = 2000,
			Step = 1,
			Value = 400,
			Prefix = "max ",
			Suffix = "steps",
		};
		vbox.AddChild(bakeStepsBox);

		var row3 = new HBoxContainer();
		vbox.AddChild(row3);
		AddButton(row3, "Bake", OnBake);
		AddButton(row3, "Save bake", OnSaveBake);
		playButton = AddButton(row3, "Play", OnTogglePlay);
		AddButton(row3, "Stop", OnStop);

		status = new Label { Text = "Select a key to edit its targets." };
		status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(status);

		saveDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.SaveFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; IK Animation"],
		};
		saveDialog.FileSelected += path =>
		{
			try
			{
				FlushActiveKey();
				PullClipSettingsFromUi();
				Data.SortByTime();
				IkAnimation.Save(Data, path);
				RefreshKeyList();
				status.Text = $"Saved {path} ({Data.Keys.Count} keys)";
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		layer.AddChild(saveDialog);

		loadDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; IK Animation"],
		};
		loadDialog.FileSelected += path =>
		{
			try
			{
				OnStop();
				// Drop selection first so SelectKey does not Flush the old editor
				// into the freshly loaded keys.
				selectedIndex = -1;
				Data = IkAnimation.Load(path);
				baked = null;
				PushClipSettingsToUi();
				RefreshKeyList();
				if (Data.Keys.Count > 0)
					SelectKey(0);
				else
				{
					solverEditor.SetEditingVisible(false);
					status.Text = $"Loaded {path} (0 keys)";
					return;
				}
				status.Text = $"Loaded {path} ({Data.Keys.Count} keys)";
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		layer.AddChild(loadDialog);

		saveTrackDialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.SaveFile,
			Access = FileDialog.AccessEnum.Resources,
			Filters = ["*.tres ; IK Track"],
		};
		saveTrackDialog.FileSelected += path =>
		{
			try
			{
				if (baked == null)
				{
					status.Text = "Bake first.";
					return;
				}
				IkTrackAnimation.Save(baked, path);
				status.Text = $"Saved track {path} ({baked.KeyCount} keys)";
			}
			catch (Exception ex)
			{
				status.Text = ex.Message;
			}
		};
		layer.AddChild(saveTrackDialog);

		RefreshKeyList();
	}

	static Button AddButton(Control parent, string text, Action onPressed)
	{
		var b = new Button { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		b.Pressed += onPressed;
		parent.AddChild(b);
		return b;
	}

	void RefreshKeyList()
	{
		syncingUi = true;
		keyList.Clear();
		for (int i = 0; i < Data.Keys.Count; i++)
		{
			IkAnimKey k = GetKey(i);
			int n = k.TargetSet?.Targets?.Count ?? 0;
			keyList.AddItem($"[{i}] t={k.Time:0.##}s  ({n} targets)");
		}
		if (selectedIndex >= 0 && selectedIndex < Data.Keys.Count)
			keyList.Select(selectedIndex);
		syncingUi = false;
	}

	void OnKeySelected(long index)
	{
		if (syncingUi) return;
		SelectKey((int)index);
	}

	void SelectKey(int index)
	{
		if (Playing)
			OnStop();

		if (HasActiveKey && solverSetup)
			FlushActiveKey();

		selectedIndex = index;
		if (!HasActiveKey)
		{
			solverEditor.SetEditingVisible(false);
			status.Text = "No key selected.";
			return;
		}

		IkAnimKey key = GetKey(selectedIndex);
		syncingUi = true;
		timeBox.Value = key.Time;
		syncingUi = false;

		if (!solverSetup)
		{
			solverEditor.Setup(rig.Skeleton, key.TargetSet);
			solverSetup = true;
		}
		else
			solverEditor.ReplaceData(key.TargetSet);

		// Live-edit the same resource instance stored on the key.
		key.TargetSet = solverEditor.Data;
		solverEditor.SetEditingVisible(true);
		RefreshKeyList();
		status.Text = $"Editing key[{selectedIndex}] t={key.Time:0.##}s";
	}

	void OnTimeChanged(double value)
	{
		if (syncingUi || !HasActiveKey) return;
		IkAnimKey key = GetKey(selectedIndex);
		key.Time = (float)value;
		ResortKeysKeepingSelection(key);
		status.Text = $"key[{selectedIndex}] t={key.Time:0.##}s";
	}

	/// <summary>Sort by time and keep selection on the same key instance.</summary>
	void ResortKeysKeepingSelection(IkAnimKey key)
	{
		Data.SortByTime();
		selectedIndex = Data.IndexOf(key);
		RefreshKeyList();
	}

	void OnCopyPose()
	{
		FlushActiveKey();
		poseClipboard = IkTargetSet.Clone(solverEditor.Data);
		status.Text = $"Copied pose ({poseClipboard.Targets.Count} targets)";
	}

	void OnPastePose()
	{
		PastePose(mirrored: false);
	}

	void OnPastePoseMirrored()
	{
		PastePose(mirrored: true);
	}

	void PastePose(bool mirrored)
	{
		IkTargetSet pasted = mirrored
			? IkTargetSet.CloneMirrored(poseClipboard)
			: IkTargetSet.Clone(poseClipboard);
		IkAnimKey key = GetKey(selectedIndex);
		key.TargetSet = pasted;
		solverEditor.ReplaceData(pasted);
		key.TargetSet = solverEditor.Data;
		RefreshKeyList();
		string tag = mirrored ? "mirrored " : "";
		status.Text = $"Pasted {tag}pose onto key[{selectedIndex}] ({pasted.Targets.Count} targets)";
	}

	void OnAddKey()
	{
		FlushActiveKey();
		float t = Data.Keys.Count == 0 ? 0f : GetKey(Data.Keys.Count - 1).Time + 1f;
		IkTargetSet solver;
		if (HasActiveKey)
			solver = IkTargetSet.Clone(solverEditor.Data);
		else
		{
			solver = new IkTargetSet();
			SeedRestTarget(solver, "toe.L");
			SeedRestTarget(solver, "toe.R");
		}
		Data.AddKey(t, solver);
		IkAnimKey added = GetKey(Data.Keys.Count - 1);
		Data.SortByTime();
		SelectKey(Data.IndexOf(added));
		status.Text = $"Added key[{selectedIndex}] t={t:0.##}s";
	}

	void OnDuplicateKey()
	{
		FlushActiveKey();
		IkAnimKey src = GetKey(selectedIndex);
		Data.AddKey(src.Time + 0.5f, src.TargetSet);
		IkAnimKey added = GetKey(Data.Keys.Count - 1);
		Data.SortByTime();
		SelectKey(Data.IndexOf(added));
		status.Text = $"Duplicated → key[{selectedIndex}]";
	}

	void OnRemoveKey()
	{
		int remove = selectedIndex;
		selectedIndex = -1;
		Data.RemoveAt(remove);
		baked = null;
		if (Data.Keys.Count == 0)
		{
			solverEditor.SetEditingVisible(false);
			RefreshKeyList();
			status.Text = "No keys left.";
			return;
		}
		SelectKey(Mathf.Clamp(remove, 0, Data.Keys.Count - 1));
		status.Text = $"Removed key; now [{selectedIndex}]";
	}

	void OnBake()
	{
		FlushActiveKey();
		PullClipSettingsFromUi();
		Data.SortByTime();
		RefreshKeyList();

		int steps = (int)bakeStepsBox.Value;
		baked = IkTrackAnimation.Bake(Data, rig, terms, steps);
		Playing = false;
		playButton.Text = "Play";

		int show = HasActiveKey ? selectedIndex : 0;
		show = Mathf.Clamp(show, 0, Data.Keys.Count - 1);
		baked.ApplyKey(rig.Skeleton, show);
		status.Text = $"Baked {baked.KeyCount} keys × {steps} steps (Save bake to keep)";
	}

	void OnSaveBake()
	{
		if (baked == null)
		{
			status.Text = "Bake first.";
			return;
		}
		saveTrackDialog.CurrentPath = DefaultTrackPath;
		saveTrackDialog.PopupCentered();
	}

	void OnTogglePlay()
	{
		Playing = !Playing;
		if (Playing)
		{
			FlushActiveKey();
			PullClipSettingsFromUi();
			playTime = 0f;
			solverEditor.SetEditingVisible(false);
			playButton.Text = "Pause";
			baked.PlayAt(rig.Skeleton, playTime);
			string mode = baked.Cyclic ? "cyclic" : "once";
			status.Text = $"Playing baked {mode} ({baked.KeyCount} keys, Catmull–Rom)…";
		}
		else
		{
			playButton.Text = "Play";
			if (HasActiveKey)
				solverEditor.SetEditingVisible(true);
			status.Text = "Paused.";
		}
	}

	void OnStop()
	{
		Playing = false;
		playTime = 0f;
		playButton.Text = "Play";
		if (HasActiveKey)
			solverEditor.SetEditingVisible(true);
		status.Text = "Stopped.";
	}
}
