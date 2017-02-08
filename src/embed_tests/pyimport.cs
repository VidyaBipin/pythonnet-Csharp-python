using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;
using System.IO;

namespace Python.EmbeddingTest
{
    [TestFixture]
    public class PyImportTest
    {
        private IntPtr gs;

        [SetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
            gs = PythonEngine.AcquireLock();

            /* Append the tests directory to sys.path
             * using reflection to circumvent the private
             * modifiers placed on most Runtime methods.
             */
            const string s = @"../../tests";

            string testPath = Path.Combine(TestContext.CurrentContext.TestDirectory, s);

            IntPtr str = Runtime.Runtime.PyString_FromString(testPath);
            IntPtr path = Runtime.Runtime.PySys_GetObject("path");
            Runtime.Runtime.PyList_Append(path, str);
        }

        [TearDown]
        public void TearDown()
        {
            PythonEngine.ReleaseLock(gs);
            PythonEngine.Shutdown();
        }

        /// <summary>
        /// Test subdirectory import
        /// </summary>
        /// <remarks>
        /// The required directory structure was added to .\pythonnet\src\tests\ directory:
        /// + PyImportTest/
        /// | - __init__.py
        /// | + test/
        /// | | - __init__.py
        /// | | - one.py
        /// </remarks>
        [Test]
        public void TestDottedName()
        {
            PyObject module1 = PythonEngine.ImportModule("PyImportTest.test.one");
            Assert.IsNotNull(module1, ">>>  import PyImportTest.test.one  # FAILED");
            PyObject module2 = PythonEngine.ImportModule("PyImportTest.sysargv");
            Assert.IsNotNull(module2, ">>>  import PyImportTest.sysargv  # FAILED");
        }
    }
}
