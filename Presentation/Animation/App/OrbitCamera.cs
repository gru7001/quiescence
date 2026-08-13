using Godot;

public partial class OrbitCamera : Camera3D
{
	[Export] public NodePath TargetPath { get; set; } = new("../Qbody");
	[Export] public float Distance { get; set; } = 2.5f;
	[Export] public float MinDistance { get; set; } = 0.8f;
	[Export] public float MaxDistance { get; set; } = 6f;
	[Export] public float OrbitSpeed { get; set; } = 0.005f;
	[Export] public float ZoomSpeed { get; set; } = 0.15f;
	[Export] public float MinPitch { get; set; } = -1.2f;
	[Export] public float MaxPitch { get; set; } = 1.2f;
	/// <summary>Mouse button used to orbit (clothing editor uses Right so Left can drag markers).</summary>
	[Export] public MouseButton OrbitButton { get; set; } = MouseButton.Left;

	private Node3D _target;
	private float _yaw;
	private float _pitch;
	private bool _dragging;

	public override void _Ready()
	{
		_target = GetNodeOrNull<Node3D>(TargetPath);
		Vector3 offset = GlobalPosition - (_target?.GlobalPosition ?? Vector3.Zero);
		Distance = offset.Length();
		if (Distance > 1e-4f)
		{
			_pitch = Mathf.Asin(Mathf.Clamp(offset.Y / Distance, -1f, 1f));
			_yaw = Mathf.Atan2(offset.X, offset.Z);
		}
		UpdateTransform();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		bool changed = false;

		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == OrbitButton)
				_dragging = mouseButton.Pressed;
			else if (mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				Distance = Mathf.Clamp(Distance - ZoomSpeed, MinDistance, MaxDistance);
				changed = true;
			}
			else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				Distance = Mathf.Clamp(Distance + ZoomSpeed, MinDistance, MaxDistance);
				changed = true;
			}
		}
		else if (@event is InputEventMouseMotion motion && _dragging)
		{
			_yaw -= motion.Relative.X * OrbitSpeed;
			_pitch = Mathf.Clamp(_pitch - motion.Relative.Y * OrbitSpeed, MinPitch, MaxPitch);
			changed = true;
		}

		if (changed)
			UpdateTransform();
	}

	private void UpdateTransform()
	{
		Vector3 target = (_target?.GlobalPosition ?? Vector3.Zero) + new Vector3(0f, 1f, 0f);
		float cosPitch = Mathf.Cos(_pitch);
		Vector3 offset = new Vector3(
			Mathf.Sin(_yaw) * cosPitch,
			Mathf.Sin(_pitch),
			Mathf.Cos(_yaw) * cosPitch
		) * Distance;

		GlobalPosition = target + offset;
		LookAt(target);
	}
}
