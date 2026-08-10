using Godot;

public partial class SaveLoadUi : Control
{
	private Main _main = null!;

	public override void _Ready()
	{
		// This scene root is full-rect. Make the overlay click-through except for the actual panel,
		// otherwise it will block gameplay/seat UI underneath.
		MouseFilter = MouseFilterEnum.Ignore;
		GetNode<Control>("Panel").MouseFilter = MouseFilterEnum.Stop;

		_main =
			GetTree().Root.GetNodeOrNull<Main>("Game")
			?? GetTree().Root.GetNodeOrNull<Main>("Main")
			?? GetParentOrNull<Main>()
			?? throw new System.InvalidOperationException("SaveLoadUi couldn't find Main node.");

		// Use explicit paths instead of "%Name" unique-name lookup; unique-name resolution can
		// break depending on owner/reparenting details of instantiated scenes.
		var save = GetNodeOrNull<Button>("Panel/VBox/SaveButton");
		var load = GetNodeOrNull<Button>("Panel/VBox/LoadButton");
		if (save == null || load == null)
		{
			GD.PushError("SaveLoadUi couldn't find SaveButton/LoadButton at Panel/VBox/*.");
			return;
		}

		save.Pressed += () => _main.SaveToDefaultPath();
		load.Pressed += () => _main.LoadFromDefaultPath();
	}
}
