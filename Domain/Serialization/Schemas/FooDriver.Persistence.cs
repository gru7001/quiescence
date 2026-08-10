public static class FooDriverPersistence
{
	public const string SaveSchemaId = "fooDriver.v1";

	public sealed record DriverSave(long WaitDeltaTicks);

	public static DriverSave Encode(FooDriver d) => new(d.WaitDeltaTicks);

	public static void Apply(FooDriver _, DriverSave __, LoadSession ___) { }
}
