using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class ActualConsoleTests
    {
        // IDE0330: keep object here because this test project also targets net8.0.
        private static readonly object ConsoleRedirectionLock = new();

        [TestMethod]
        public void ReadLine_ReturnsConsoleInput()
        {
            lock (ConsoleRedirectionLock)
            {
                var originalIn = Console.In;

                try
                {
                    Console.SetIn(new StringReader("hello" + Environment.NewLine));

                    var console = new ActualConsole();

                    console.ReadLine().ShouldBe("hello");
                }
                finally
                {
                    Console.SetIn(originalIn);
                }
            }
        }

        [TestMethod]
        public void WriteLine_WritesToConsoleOutput()
        {
            lock (ConsoleRedirectionLock)
            {
                var originalOut = Console.Out;
                using var writer = new StringWriter();

                try
                {
                    Console.SetOut(writer);

                    var console = new ActualConsole();

                    console.WriteLine("hello");

                    writer.ToString().ShouldBe("hello" + Environment.NewLine);
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }
    }
}
