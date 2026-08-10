public static class LegacyGodotSeatDriverPersistence
{
	public const string SaveSchemaId = "godotSeatDriver.v1";

	/// <summary>No fields yet; reserved for future seat/UI state.</summary>
	public sealed record DriverSave;

	public static DriverSave Encode(LegacyGodotSeatDriver _) => new();

	public static void Apply(LegacyGodotSeatDriver _, DriverSave __, LoadSession ___) { }
}
