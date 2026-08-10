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

	private Node3D _target;
	private float _yaw;
	private float _pitch;
	private bool _dragging;

	public override void _Ready()
	{
		_target = GetNode<Node3D>(TargetPath);
		Vector3 offset = GlobalPosition - _target.GlobalPosition;
		Distance = offset.Length();
		_pitch = Mathf.Asin(offset.Y / Distance);
		_yaw = Mathf.Atan2(offset.X, offset.Z);
		UpdateTransform();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		bool changed = false;

		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
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
		if (_target == null)
			return;

		Vector3 target = _target.GlobalPosition + new Vector3(0f, 1f, 0f);
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
