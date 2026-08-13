using DelaunyFabric.View;
using Godot;

/// <summary>Scene root for the clothing authoring / sim / finish editor.</summary>
public partial class ClothingEditorApp : Node3D
{
	ClothingEditor editor;

	public override void _Ready()
	{
		var body = GetNode<Node3D>("Qbody");
		var camera = GetNode<Camera3D>("Camera3D");

		editor = new ClothingEditor { Name = "ClothingEditor" };
		AddChild(editor);
		editor.Setup(body, camera);
	}
}
