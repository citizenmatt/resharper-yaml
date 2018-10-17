using JetBrains.Application.BuildScript.Application.Zones;
using JetBrains.ReSharper.TestFramework;
using JetBrains.TestFramework;
using JetBrains.TestFramework.Application.Zones;
using NUnit.Framework;

[assembly: RequiresSTA]

#pragma warning disable 618
[assembly: TestDataPathBase("resharper/test/data")]
#pragma warning restore 618

namespace JetBrains.ReSharper.Plugins.Yaml.Tests
{
    [ZoneDefinition]
    public interface IYamlTestZone : ITestsEnvZone, IRequire<PsiFeatureTestZone>
    {
    }

    [SetUpFixture]
    public class TestEnvironment : ExtensionTestEnvironmentAssembly<IYamlTestZone>
    {
    }
}
