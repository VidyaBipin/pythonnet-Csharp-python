using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest;

public class BaseFixture
{
    [OneTimeSetUp]
    public void BaseSetup()
    {
        PythonEngine.Initialize();
    }

    [OneTimeTearDown]
    public void BaseTearDown()
    {
        PyObjectConversions.Reset();
        PythonEngine.Shutdown(allowReload: true);
    }
}
