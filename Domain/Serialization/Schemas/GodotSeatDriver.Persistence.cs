public static class GodotSeatDriverPersistence
{
	public const string SaveSchemaId = "godotSeatDriver.v3";

	public sealed record DriverSave(NodeRef Clock);

	public static DriverSave Encode(GodotSeatDriver seat, SaveSession session) =>
		new(Clock: session.Ref(seat.Clock));

	public static void Apply(GodotSeatDriver _, DriverSave __, LoadSession ___) { }
}
