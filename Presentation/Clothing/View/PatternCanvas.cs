using System.Collections.Generic;
using DelaunyFabric.Core;
using Godot;

namespace DelaunyFabric.View;

/// <summary>2D UV view. Picks nodes and forwards intent to <see cref="PatternSession"/>.</summary>
public partial class PatternCanvas : Control
{
	public PatternSession Session { get; set; } = new();

	bool _boxing;
	bool _dragging;
	Vector2 _boxStart;
	Vector2 _boxEnd;
	Vector2 _dragStartMouseUv;
	readonly Dictionary<int, Vector2> _dragStartUvs = new();

	const float NodeRadius = 8f;

	GarmentPattern Pattern => Session?.Pattern;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(460, 460);
		SizeFlagsHorizontal = SizeFlags.Fill;
		SizeFlagsVertical = SizeFlags.Fill;
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.Click;
		ClipContents = true;
	}

	public override void _Draw()
	{
		var size = Size;
		DrawRect(new Rect2(Vector2.Zero, size), new Color(0.08f, 0.08f, 0.1f, 1f));
		DrawRect(new Rect2(Vector2.Zero, size), new Color(0.25f, 0.25f, 0.3f, 1f), false, 1f);
		if (Session.SnapEnabled && Session.SnapDivisions > 1)
			DrawSnapGrid();

		if (Pattern?.Edges != null)
		{
			foreach (Variant v in Pattern.Edges)
			{
				if (v.AsGodotObject() is not GarmentEdge e)
					continue;
				if (!TryScreen(e.A, out var a) || !TryScreen(e.B, out var b))
					continue;
				DrawLine(a, b, new Color(0.55f, 0.7f, 1f, 1f), 2f);
			}
		}

		if (Pattern?.Sews != null)
		{
			foreach (Variant v in Pattern.Sews)
			{
				if (v.AsGodotObject() is not GarmentSew s)
					continue;
				if (!TryScreen(s.A, out var a) || !TryScreen(s.B, out var b))
					continue;
				DrawDashedLine(a, b, new Color(1f, 0.55f, 0.35f, 1f), 2f);
			}
		}

		if (Pattern?.Nodes != null)
		{
			for (int i = 0; i < Pattern.Nodes.Count; i++)
			{
				if (!TryScreen(i, out var p))
					continue;
				bool sel = Session.IsSelected(i);
				var fill = sel ? new Color(0.95f, 0.85f, 0.3f) : new Color(0.85f, 0.85f, 0.9f);
				DrawCircle(p, NodeRadius, fill);
				DrawArc(p, NodeRadius, 0f, Mathf.Tau, 24, Colors.Black, 1f);
				DrawString(ThemeDB.FallbackFont, p + new Vector2(10, -6), i.ToString(),
					HorizontalAlignment.Left, -1, 12);
			}
		}

		if (Session.LinkFrom >= 0 && TryScreen(Session.LinkFrom, out var from))
			DrawCircle(from, NodeRadius + 3f, new Color(0.3f, 1f, 0.5f, 0.35f));

		if (_boxing)
		{
			var rect = BoxRect(_boxStart, _boxEnd);
			DrawRect(rect, new Color(0.4f, 0.7f, 1f, 0.15f));
			DrawRect(rect, new Color(0.5f, 0.8f, 1f, 0.9f), false, 1f);
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (Session?.Pattern == null)
			return;

		if (@event is InputEventKey { Pressed: true, Echo: false } key && Session.Mode == PatternEditMode.Select)
		{
			if (key.CtrlPressed && key.Keycode == Key.C)
			{
				Session.CopySelection();
				AcceptEvent();
				return;
			}
			if (key.CtrlPressed && key.Keycode == Key.V)
			{
				Session.PasteClipboard(mirrorX: key.ShiftPressed);
				AcceptEvent();
				return;
			}
			if (key.Keycode == Key.Delete || key.Keycode == Key.Backspace)
			{
				Session.DeleteSelection();
				AcceptEvent();
				return;
			}
			if (key.Keycode == Key.Key0)
			{
				Session.ZeroSelectionOffset();
				AcceptEvent();
				return;
			}
		}

		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
		{
			if (mb.Pressed)
				OnLeftPressed(mb.Position, mb.ShiftPressed);
			else
				OnLeftReleased(mb.Position, mb.ShiftPressed);
			AcceptEvent();
			QueueRedraw();
			return;
		}

		if (@event is InputEventMouseMotion motion
		    && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
		{
			if (_boxing && Session.Mode == PatternEditMode.Select)
			{
				_boxEnd = motion.Position;
				QueueRedraw();
				AcceptEvent();
				return;
			}

			if (_dragging && Session.Mode == PatternEditMode.Select && Session.Selection.Count > 0)
			{
				var delta = ScreenToUv(motion.Position) - _dragStartMouseUv;
				foreach (int i in Session.Selection)
				{
					if (!_dragStartUvs.TryGetValue(i, out var start))
						continue;
					if (i < 0 || i >= Pattern.Nodes.Count)
						continue;
					Pattern.Nodes[i].Uv = Session.SnapUv(start + delta);
				}
				Session.NotifyPatternChanged();
				QueueRedraw();
				AcceptEvent();
			}
		}
	}

	void OnLeftPressed(Vector2 local, bool shift)
	{
		int hit = HitTest(local);
		_boxing = false;
		_dragging = false;

		if (Session.Mode == PatternEditMode.AddNode)
		{
			Session.AddNodeAt(ScreenToUv(local));
			return;
		}

		if (hit >= 0)
		{
			Session.HitNode(hit, shift);
			if (Session.Mode == PatternEditMode.Select)
			{
				_dragging = true;
				_dragStartMouseUv = ScreenToUv(local);
				_dragStartUvs.Clear();
				foreach (int i in Session.Selection)
				{
					if (i >= 0 && i < Pattern.Nodes.Count)
						_dragStartUvs[i] = Pattern.Nodes[i].Uv;
				}
			}
			return;
		}

		Session.Miss(shift);
		if (Session.Mode == PatternEditMode.Select)
		{
			_boxing = true;
			_boxStart = local;
			_boxEnd = local;
		}
	}

	void OnLeftReleased(Vector2 local, bool shift)
	{
		if (_boxing)
		{
			_boxEnd = local;
			var rect = BoxRect(_boxStart, _boxEnd);
			var hits = new List<int>();
			if (Pattern?.Nodes != null)
			{
				for (int i = 0; i < Pattern.Nodes.Count; i++)
				{
					if (!TryScreen(i, out var p))
						continue;
					if (rect.HasPoint(p))
						hits.Add(i);
				}
			}

			if (shift)
				Session.AddToSelection(hits);
			else
				Session.SetSelection(hits, hits.Count > 0 ? hits[^1] : -1);
			_boxing = false;
		}
		_dragging = false;
		_dragStartUvs.Clear();
	}

	static Rect2 BoxRect(Vector2 a, Vector2 b)
	{
		var pos = new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y));
		var size = new Vector2(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
		return new Rect2(pos, size);
	}

	void DrawSnapGrid()
	{
		int n = Mathf.Max(2, Session.SnapDivisions);
		var color = new Color(0.22f, 0.22f, 0.28f, 0.9f);
		for (int i = 0; i <= n; i++)
		{
			float t = i / (float)n;
			DrawLine(UvToScreen(new Vector2(t, 0f)), UvToScreen(new Vector2(t, 1f)), color, 1f);
			DrawLine(UvToScreen(new Vector2(0f, t)), UvToScreen(new Vector2(1f, t)), color, 1f);
		}
	}

	int HitTest(Vector2 screen)
	{
		if (Pattern?.Nodes == null)
			return -1;
		for (int i = Pattern.Nodes.Count - 1; i >= 0; i--)
		{
			if (!TryScreen(i, out var p))
				continue;
			if (p.DistanceTo(screen) <= NodeRadius + 4f)
				return i;
		}
		return -1;
	}

	bool TryScreen(int index, out Vector2 screen)
	{
		screen = default;
		if (Pattern?.Nodes == null || index < 0 || index >= Pattern.Nodes.Count)
			return false;
		screen = UvToScreen(Pattern.Nodes[index].Uv);
		return true;
	}

	Vector2 UvToScreen(Vector2 uv)
	{
		float pad = 16f;
		return new Vector2(
			pad + uv.X * (Size.X - pad * 2f),
			pad + (1f - uv.Y) * (Size.Y - pad * 2f));
	}

	Vector2 ScreenToUv(Vector2 screen)
	{
		float pad = 16f;
		float w = Mathf.Max(1f, Size.X - pad * 2f);
		float h = Mathf.Max(1f, Size.Y - pad * 2f);
		float x = (screen.X - pad) / w;
		float y = 1f - (screen.Y - pad) / h;
		return new Vector2(x, y);
	}
}
