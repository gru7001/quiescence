using System;
using Godot;

/// <summary>
/// Godot-side file IO helpers for user-writable data (user://).
/// Keeps filesystem concerns outside the domain model.
/// </summary>
public static class GodotUserFiles
{
	/// <summary>
	/// Writes <paramref name="text"/> to a <c>user://</c> path, creating parent directories as needed.
	/// </summary>
	public static void WriteAllText(string userPath, string text)
	{
		if (string.IsNullOrWhiteSpace(userPath))
			throw new ArgumentException("Path is required.", nameof(userPath));
		if (!userPath.StartsWith("user://", StringComparison.Ordinal))
			throw new ArgumentException("Path must start with 'user://'.", nameof(userPath));

		var abs = ProjectSettings.GlobalizePath(userPath);
		var dirAbs = System.IO.Path.GetDirectoryName(abs);
		if (!string.IsNullOrEmpty(dirAbs))
		{
			var dirUser = ProjectSettings.LocalizePath(dirAbs);
			DirAccess.MakeDirRecursiveAbsolute(dirUser);
		}

		using var f = FileAccess.Open(userPath, FileAccess.ModeFlags.Write);
		if (f == null)
			throw new InvalidOperationException($"Failed to open '{userPath}' for writing.");
		f.StoreString(text ?? "");
	}

	public static string ReadAllText(string userPath)
	{
		if (string.IsNullOrWhiteSpace(userPath))
			throw new ArgumentException("Path is required.", nameof(userPath));
		if (!userPath.StartsWith("user://", StringComparison.Ordinal))
			throw new ArgumentException("Path must start with 'user://'.", nameof(userPath));

		using var f = FileAccess.Open(userPath, FileAccess.ModeFlags.Read);
		if (f == null)
			throw new InvalidOperationException($"Failed to open '{userPath}' for reading.");
		return f.GetAsText();
	}
}

