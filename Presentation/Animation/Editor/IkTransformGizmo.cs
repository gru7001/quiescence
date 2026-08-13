using Godot;

/// <summary>
/// Runtime world-space transform gizmo for a bound Node3D.
/// Translate: axis arrows. Rotate: axis rings. LMB drag; marks input handled so orbit skips.
/// </summary>
public partial class IkTransformGizmo : Node3D
{
	public enum GizmoMode
	{
		Translate,
		Rotate,
	}

	public GizmoMode Mode { get; set; } = GizmoMode.Translate;
	public Node3D Bound { get; set; }
	/// <summary>World-space panel outward; zero hides the marker.</summary>
	public Vector3 Outward { get; set; }
	public bool Busy { get; private set; }

	const float AxisLen = 0.36f;
	const float HitPad = 0.07f;
	const float RingRadius = 0.28f;

	Node3D translateRoot;
	Node3D rotateRoot;
	Node3D outwardRoot;
	Camera3D camera;

	int dragAxis = -1; // 0=X 1=Y 2=Z
	Vector3 dragStartOrigin;
	Basis dragStartBasis;
	Vector3 dragAnchor; // world point on axis/plane at press
	float dragStartAngle;

	static readonly Color[] AxisColors =
	[
		new(1f, 0.2f, 0.2f),
		new(0.2f, 1f, 0.2f),
		new(0.2f, 0.6f, 1f),
	];

	public override void _Ready()
	{
		translateRoot = BuildTranslate();
		rotateRoot = BuildRotate();
		outwardRoot = BuildOutward();
		AddChild(translateRoot);
		AddChild(rotateRoot);
		AddChild(outwardRoot);
		UpdateModeVisibility();
	}

	public void SetMode(GizmoMode mode)
	{
		Mode = mode;
		UpdateModeVisibility();
	}

	void UpdateModeVisibility()
	{
		if (translateRoot != null) translateRoot.Visible = Mode == GizmoMode.Translate;
		if (rotateRoot != null) rotateRoot.Visible = Mode == GizmoMode.Rotate;
	}

	public override void _Process(double delta)
	{
		camera ??= GetViewport().GetCamera3D();
		if (Bound == null || !GodotObject.IsInstanceValid(Bound))
		{
			Visible = false;
			Busy = false;
			return;
		}

		Visible = true;
		GlobalPosition = Bound.GlobalPosition;
		GlobalBasis = Basis.Identity; // world-space handles
		UpdateOutwardArrow();

		if (camera != null)
		{
			float dist = GlobalPosition.DistanceTo(camera.GlobalPosition);
			Scale = Vector3.One * Mathf.Clamp(dist * 0.18f, 0.5f, 3.5f);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (Bound == null || camera == null || !Visible) return;

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				if (TryPick(mb.Position, out int axis, out Vector3 anchor, out float angle))
				{
					dragAxis = axis;
					dragStartOrigin = Bound.GlobalPosition;
					dragStartBasis = Bound.GlobalBasis;
					dragAnchor = anchor;
					dragStartAngle = angle;
					Busy = true;
					GetViewport().SetInputAsHandled();
				}
			}
			else if (Busy)
			{
				Busy = false;
				dragAxis = -1;
				GetViewport().SetInputAsHandled();
			}
		}
		else if (@event is InputEventMouseMotion motion && Busy && dragAxis >= 0)
		{
			if (Mode == GizmoMode.Translate)
				DragTranslate(motion.Position);
			else
				DragRotate(motion.Position);
			GetViewport().SetInputAsHandled();
		}
	}

	void DragTranslate(Vector2 screen)
	{
		Vector3 axis = AxisVector(dragAxis);
		if (!Ray(screen, out Vector3 ro, out Vector3 rd)) return;
		if (!ClosestOnAxis(ro, rd, dragStartOrigin, axis, out Vector3 hit)) return;
		Bound.GlobalPosition = dragStartOrigin + axis * (dragAnchor - hit).Dot(axis);
	}

	void DragRotate(Vector2 screen)
	{
		Vector3 axis = AxisVector(dragAxis);
		float angle = AngleAroundAxis(screen, dragStartOrigin, axis);
		float delta = angle - dragStartAngle;
		Bound.GlobalBasis = new Basis(axis, delta) * dragStartBasis;
		Bound.GlobalPosition = dragStartOrigin;
	}

	bool TryPick(Vector2 screen, out int axis, out Vector3 anchor, out float angle)
	{
		axis = -1;
		anchor = Vector3.Zero;
		angle = 0f;
		if (!Ray(screen, out Vector3 ro, out Vector3 rd)) return false;

		float best = HitPad * Scale.X;
		bool hit = false;

		if (Mode == GizmoMode.Translate)
		{
			for (int a = 0; a < 3; a++)
			{
				Vector3 o = GlobalPosition;
				Vector3 ax = AxisVector(a);
				float dist = RaySegmentDistance(ro, rd, o, o + ax * AxisLen * Scale.X, out _);
				if (dist < best)
				{
					best = dist;
					axis = a;
					hit = true;
				}
			}
			// Same projection as DragTranslate — avoids first-frame snap.
			if (hit)
				ClosestOnAxis(ro, rd, GlobalPosition, AxisVector(axis), out anchor);
		}
		else
		{
			for (int a = 0; a < 3; a++)
			{
				Vector3 ax = AxisVector(a);
				float dist = RayCircleDistance(ro, rd, GlobalPosition, ax, RingRadius * Scale.X, out Vector3 onCircle);
				if (dist < best)
				{
					best = dist;
					axis = a;
					anchor = onCircle;
					angle = AngleAroundAxis(screen, GlobalPosition, ax);
					hit = true;
				}
			}
		}

		return hit;
	}

	float AngleAroundAxis(Vector2 screen, Vector3 center, Vector3 axis)
	{
		if (!Ray(screen, out Vector3 ro, out Vector3 rd)) return 0f;
		// Intersect ray with plane through center, normal = axis
		float denom = rd.Dot(axis);
		if (Mathf.Abs(denom) < 1e-6f) return 0f;
		float t = (center - ro).Dot(axis) / denom;
		Vector3 p = ro + rd * t;
		Vector3 v = p - center;
		Vector3 refDir = Mathf.Abs(axis.Dot(Vector3.Up)) < 0.9f
			? axis.Cross(Vector3.Up).Normalized()
			: axis.Cross(Vector3.Right).Normalized();
		Vector3 bitan = axis.Cross(refDir);
		return Mathf.Atan2(v.Dot(bitan), v.Dot(refDir));
	}

	bool Ray(Vector2 screen, out Vector3 origin, out Vector3 dir)
	{
		origin = camera.ProjectRayOrigin(screen);
		dir = camera.ProjectRayNormal(screen);
		return dir.LengthSquared() > 1e-12f;
	}

	static Vector3 AxisVector(int a) => a switch
	{
		0 => Vector3.Right,
		1 => Vector3.Up,
		_ => Vector3.Back, // +Z world; rings/arrows use Z
	};

	static bool ClosestOnAxis(Vector3 ro, Vector3 rd, Vector3 axisOrigin, Vector3 axisDir, out Vector3 point)
	{
		// Closest point on infinite axis to the ray, then use that as drag sample
		axisDir = axisDir.Normalized();
		Vector3 w0 = axisOrigin - ro;
		float a = 1f;
		float b = rd.Dot(axisDir);
		float c = axisDir.Dot(axisDir);
		float d = rd.Dot(w0);
		float e = axisDir.Dot(w0);
		float denom = a * c - b * b;
		float tAxis;
		if (Mathf.Abs(denom) < 1e-8f)
			tAxis = 0f;
		else
			tAxis = (a * e - b * d) / denom;
		point = axisOrigin + axisDir * tAxis;
		return true;
	}

	static float RaySegmentDistance(Vector3 ro, Vector3 rd, Vector3 a, Vector3 b, out Vector3 onSeg)
	{
		Vector3 u = rd;
		Vector3 v = b - a;
		Vector3 w = ro - a;
		float uu = u.Dot(u);
		float uv = u.Dot(v);
		float vv = v.Dot(v);
		float uw = u.Dot(w);
		float vw = v.Dot(w);
		float denom = uu * vv - uv * uv;
		float s, t;
		if (Mathf.Abs(denom) < 1e-8f)
		{
			s = 0f;
			t = vv > 1e-8f ? vw / vv : 0f;
		}
		else
		{
			s = (uv * vw - vv * uw) / denom;
			t = (uu * vw - uv * uw) / denom;
		}
		t = Mathf.Clamp(t, 0f, 1f);
		onSeg = a + v * t;
		Vector3 onRay = ro + u * s;
		return onRay.DistanceTo(onSeg);
	}

	static float RayCircleDistance(Vector3 ro, Vector3 rd, Vector3 center, Vector3 normal, float radius, out Vector3 onCircle)
	{
		normal = normal.Normalized();
		float denom = rd.Dot(normal);
		onCircle = center;
		if (Mathf.Abs(denom) < 1e-6f) return float.MaxValue;
		float t = (center - ro).Dot(normal) / denom;
		if (t < 0f) return float.MaxValue;
		Vector3 hit = ro + rd * t;
		Vector3 radial = hit - center;
		radial -= normal * radial.Dot(normal);
		float len = radial.Length();
		if (len < 1e-8f) return float.MaxValue;
		onCircle = center + radial * (radius / len);
		return Mathf.Abs(len - radius);
	}

	Node3D BuildTranslate()
	{
		var root = new Node3D { Name = "Translate" };
		for (int a = 0; a < 3; a++)
		{
			Vector3 axis = AxisVector(a);
			var shaft = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0.014f, BottomRadius = 0.014f, Height = AxisLen * 0.85f },
				MaterialOverride = Unshaded(AxisColors[a]),
			};
			// Cylinder default along Y; align to axis
			shaft.Position = axis * (AxisLen * 0.425f);
			shaft.Basis = BasisFromY(axis);
			root.AddChild(shaft);

			var tip = new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.036f, Height = AxisLen * 0.15f },
				MaterialOverride = Unshaded(AxisColors[a]),
			};
			tip.Position = axis * (AxisLen * 0.925f);
			tip.Basis = BasisFromY(axis);
			root.AddChild(tip);
		}
		return root;
	}

	Node3D BuildRotate()
	{
		var root = new Node3D { Name = "Rotate" };
		for (int a = 0; a < 3; a++)
		{
			Vector3 axis = AxisVector(a);
			var ring = new MeshInstance3D
			{
				Mesh = new TorusMesh { InnerRadius = RingRadius - 0.012f, OuterRadius = RingRadius + 0.012f },
				MaterialOverride = Unshaded(AxisColors[a]),
			};
			// Torus lies in XZ by default (around Y); align so normal = axis
			ring.Basis = BasisFromY(axis);
			root.AddChild(ring);
		}
		return root;
	}

	Node3D BuildOutward()
	{
		var root = new Node3D { Name = "Outward" };
		var color = new Color(1f, 0.45f, 0.95f);
		var shaft = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.016f, Height = AxisLen * 0.85f },
			MaterialOverride = Unshaded(color),
		};
		shaft.Position = Vector3.Up * (AxisLen * 0.425f);
		root.AddChild(shaft);

		var tip = new MeshInstance3D
		{
			Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.042f, Height = AxisLen * 0.18f },
			MaterialOverride = Unshaded(color),
		};
		tip.Position = Vector3.Up * (AxisLen * 0.94f);
		root.AddChild(tip);
		return root;
	}

	void UpdateOutwardArrow()
	{
		if (outwardRoot == null)
			return;
		if (Outward.LengthSquared() < 1e-8f)
		{
			outwardRoot.Visible = false;
			return;
		}
		outwardRoot.Visible = true;
		outwardRoot.Basis = BasisFromY(Outward.Normalized());
	}

	static Basis BasisFromY(Vector3 y) => new(new Quaternion(Vector3.Up, y.Normalized()));

	static StandardMaterial3D Unshaded(Color c) => new()
	{
		AlbedoColor = c,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		NoDepthTest = true,
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
	};
}
