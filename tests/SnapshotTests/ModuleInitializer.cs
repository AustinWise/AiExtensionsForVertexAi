using System.Runtime.CompilerServices;

namespace SnapshotTests;

internal class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        AWise.AiExtensionsForVertexAi.TestHelpers.IsRunningInUnitTest = true;
    }
}
