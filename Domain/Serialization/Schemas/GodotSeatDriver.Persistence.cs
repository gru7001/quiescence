public static class GodotSeatDriverPersistence
{
	public const string SaveSchemaId = "godotSeatDriver.v2";

	/// <summary>No fields yet; reserved for future seat/UI state.</summary>
	public sealed record DriverSave;

	public static DriverSave Encode(GodotSeatDriver _) => new();

	public static void Apply(GodotSeatDriver _, DriverSave __, LoadSession ___) { }
}
