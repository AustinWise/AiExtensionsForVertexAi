namespace AWise.AiExtensionsForVertexAi;

internal static class TestHelpers
{
#if DEBUG
    // The purpose of this is to remove some sources of nondeterminism when running snapshot tests.
    internal static bool IsRunningInUnitTest { get; set; }
#else
    internal static bool IsRunningInUnitTest => false;
#endif
}
