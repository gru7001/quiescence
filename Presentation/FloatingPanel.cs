using Godot;

/// <summary>Draggable, closeable chrome around a seat UI body.</summary>
public partial class FloatingPanel : PanelContainer
{
	private bool _dragging;
	private Vector2 _dragOffset;
	private readonly Label _titleLabel;
	private readonly StyleBoxFlat _style;
	private bool _passThrough;

	public Control Body { get; }

	public string Title
	{
		get => _titleLabel.Text;
		set => _titleLabel.Text = value ?? "";
	}

	public FloatingPanel(string title, Vector2 position)
	{
		Name = $"Panel_{title}";
		Position = position;
		MouseFilter = MouseFilterEnum.Stop;
		CustomMinimumSize = new Vector2(160, 40);

		_style = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.12f, 0.14f, 0.92f),
			BorderColor = new Color(0.35f, 0.35f, 0.4f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6,
			ContentMarginTop = 4,
			ContentMarginRight = 6,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4
		};
		AddThemeStyleboxOverride("panel", _style);

		var root = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
		AddChild(root);

		var bar = new HBoxContainer
		{
			Name = "TitleBar",
			MouseFilter = MouseFilterEnum.Stop
		};
		bar.GuiInput += OnBarGuiInput;

		_titleLabel = new Label
		{
			Text = title ?? "",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		bar.AddChild(_titleLabel);

		var close = new Button
		{
			Text = "×",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(28, 0)
		};
		close.Pressed += () => Visible = false;
		bar.AddChild(close);
		root.AddChild(bar);

		Body = new VBoxContainer
		{
			Name = "Body",
			MouseFilter = MouseFilterEnum.Stop
		};
		root.AddChild(Body);
	}

	public void ShowPanel()
	{
		Visible = true;
		MoveToFront();
	}

	/// <summary>Raise and ensure visible (completion focus).</summary>
	public void FocusOpen()
	{
		Visible = true;
		MoveToFront();
	}

	/// <summary>
	/// Board-lens completion: translucent and ignore mouse so clicks reach the board.
	/// </summary>
	public void SetPassThrough(bool passThrough)
	{
		if (_passThrough == passThrough)
			return;
		_passThrough = passThrough;
		if (passThrough)
		{
			_style.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.35f);
			MouseFilter = MouseFilterEnum.Ignore;
			SetSubtreeMouseFilter(this, MouseFilterEnum.Ignore);
		}
		else
		{
			_style.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);
			MouseFilter = MouseFilterEnum.Stop;
			SetSubtreeMouseFilter(this, MouseFilterEnum.Stop);
			_titleLabel.MouseFilter = MouseFilterEnum.Ignore;
		}
	}

	private static void SetSubtreeMouseFilter(Node node, MouseFilterEnum filter)
	{
		if (node is Control c)
			c.MouseFilter = filter;
		foreach (var child in node.GetChildren())
		{
			if (child is Node n)
				SetSubtreeMouseFilter(n, filter);
		}
	}

	private void OnBarGuiInput(InputEvent @event)
	{
		if (_passThrough)
			return;

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				_dragging = true;
				_dragOffset = GetGlobalMousePosition() - GlobalPosition;
				MoveToFront();
				AcceptEvent();
			}
			else if (_dragging)
			{
				_dragging = false;
				AcceptEvent();
			}
			return;
		}

		if (@event is InputEventMouseMotion && _dragging)
		{
			GlobalPosition = GetGlobalMousePosition() - _dragOffset;
			AcceptEvent();
		}
	}
}
