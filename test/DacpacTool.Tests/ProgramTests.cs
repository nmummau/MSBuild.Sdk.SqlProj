using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class ProgramTests
    {
        private const string TestProjectPath = "../../../../TestProjectWithPrePost";
        // Console.Out is process-wide; serialize redirection in these tests.
        private static readonly object ConsoleLock = new();

        [TestMethod]
        public void InspectIncludes_WritesIncludedFilesToConsoleOut()
        {
            var expectedFile = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script1.sql");
            var options = new InspectOptions
            {
                PreDeploy = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql"),
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.InspectIncludes(options);

                    result.ShouldBe(0);
                    writer.ToString().Replace("\r\n", "\n").ShouldContain($"{expectedFile.FullName}\n");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public void InspectOptions_Run_WritesIncludedFilesToConsoleOut()
        {
            var expectedFile = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script1.sql");
            var options = new InspectOptions
            {
                PreDeploy = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql"),
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = options.Run();

                    result.ShouldBe(0);
                    writer.ToString().Replace("\r\n", "\n").ShouldContain($"{expectedFile.FullName}\n");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public async Task BuildDacpac_WithSingleInputFile_BuildsPackage()
        {
            var outputPath = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var inputList = CreateInputListFile("../../../../TestProject/Tables/MyTable.sql");

            try
            {
                var options = new BuildOptions
                {
                    Name = "MyPackage",
                    Version = "1.0.0.0",
                    Output = outputPath,
                    InputFile = inputList,
                };

                var result = await Program.BuildDacpac(options);

                result.ShouldBe(0);
                File.Exists(outputPath.FullName).ShouldBeTrue();
            }
            finally
            {
                if (outputPath.Exists)
                {
                    outputPath.Delete();
                }

                if (inputList.Exists)
                {
                    inputList.Delete();
                }
            }
        }

        [TestMethod]
        public async Task BuildDacpac_GenerateCreateScript_WritesScriptFile()
        {
            var outputPath = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var inputList = CreateInputListFile("../../../../TestProject/Tables/MyTable.sql");
            var expectedScript = Path.Combine(outputPath.DirectoryName!, "TargetDb_Create.sql");

            try
            {
                var options = new BuildOptions
                {
                    Name = "MyPackage",
                    Version = "1.0.0.0",
                    Output = outputPath,
                    InputFile = inputList,
                    GenerateCreateScript = true,
                    TargetDatabaseName = "TargetDb",
                };

                var result = await Program.BuildDacpac(options);

                result.ShouldBe(0);
                File.Exists(expectedScript).ShouldBeTrue();
            }
            finally
            {
                if (outputPath.Exists)
                {
                    outputPath.Delete();
                }

                if (File.Exists(expectedScript))
                {
                    File.Delete(expectedScript);
                }

                if (inputList.Exists)
                {
                    inputList.Delete();
                }
            }
        }

        [TestMethod]
        public async Task BuildDacpac_WithSinglePartReference_BuildsPackage()
        {
            var referenceOutput = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var consumerOutput = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var referenceInputList = CreateInputListFile("../../../../TestProject/Tables/MyTable.sql");

            try
            {
                var referenceOptions = new BuildOptions
                {
                    Name = "ReferencePackage",
                    Version = "1.0.0.0",
                    Output = referenceOutput,
                    InputFile = referenceInputList,
                };

                (await Program.BuildDacpac(referenceOptions)).ShouldBe(0);
                File.Exists(referenceOutput.FullName).ShouldBeTrue();

                var consumerOptions = new BuildOptions
                {
                    Name = "ConsumerPackage",
                    Version = "1.0.0.0",
                    Output = consumerOutput,
                    Reference = [referenceOutput.FullName],
                };

                var result = await Program.BuildDacpac(consumerOptions);

                result.ShouldBe(0);
                File.Exists(consumerOutput.FullName).ShouldBeTrue();
            }
            finally
            {
                if (referenceOutput.Exists)
                {
                    referenceOutput.Delete();
                }

                if (consumerOutput.Exists)
                {
                    consumerOutput.Delete();
                }

                if (referenceInputList.Exists)
                {
                    referenceInputList.Delete();
                }
            }
        }

        [TestMethod]
        public async Task BuildDacpac_MissingInputList_Throws()
        {
            var outputPath = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var missingInputList = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt"));

            var options = new BuildOptions
            {
                Name = "MyPackage",
                Version = "1.0.0.0",
                Output = outputPath,
                InputFile = missingInputList,
            };

            (await Should.ThrowAsync<ArgumentException>(() => Program.BuildDacpac(options)))
                .Message.ShouldBe($"No input files found, missing {missingInputList.Name}");
        }

        [TestMethod]
        public async Task BuildDacpac_MalformedBuildProperty_ThrowsCleanValidationError()
        {
            var options = new BuildOptions
            {
                Name = "MyPackage",
                Version = "1.0.0.0",
                Output = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac")),
                BuildProperty = ["InvalidPropertyWithoutEquals"],
            };

            (await Should.ThrowAsync<ArgumentException>(() => Program.BuildDacpac(options)))
                .Message.ShouldBe("Expected NAME=VALUE format for --buildproperty: InvalidPropertyWithoutEquals");
        }

        [TestMethod]
        public async Task BuildDacpac_WhenInputFileHasModelException_ReturnsOne()
        {
            var outputPath = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var inputList = CreateInputListFile("../../../../TestProjectWithExceptions/Tables/MyTable2.sql");

            try
            {
                var options = new BuildOptions
                {
                    Name = "MyPackage",
                    Version = "1.0.0.0",
                    Output = outputPath,
                    InputFile = inputList,
                };

                var result = await Program.BuildDacpac(options);

                result.ShouldBe(1);
                File.Exists(outputPath.FullName).ShouldBeFalse();
            }
            finally
            {
                if (outputPath.Exists)
                {
                    outputPath.Delete();
                }

                if (inputList.Exists)
                {
                    inputList.Delete();
                }
            }
        }

        [TestMethod]
        public void InspectIncludes_WithNoScripts_WritesNothing()
        {
            var options = new InspectOptions();

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.InspectIncludes(options);

                    result.ShouldBe(0);
                    writer.ToString().ShouldBeEmpty();
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public void InspectIncludes_WithPreAndPostScripts_WritesBothIncludedFiles()
        {
            var expectedPreFile = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script1.sql");
            var expectedPostFile = new FileInfo($"{TestProjectPath}/Post-Deployment/Script1.sql");
            var options = new InspectOptions
            {
                PreDeploy = new FileInfo($"{TestProjectPath}/Pre-Deployment/Script.PreDeployment.SimpleInclude.sql"),
                PostDeploy = new FileInfo($"{TestProjectPath}/Post-Deployment/Script.PostDeployment.SimpleInclude.sql"),
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.InspectIncludes(options);
                    var output = writer.ToString().Replace("\r\n", "\n");

                    result.ShouldBe(0);
                    output.ShouldContain($"{expectedPreFile.FullName}\n");
                    output.ShouldContain($"{expectedPostFile.FullName}\n");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public void DeployDacpac_InvalidProperty_ReturnsValidationError()
        {
            var options = new DeployOptions
            {
                Input = new FileInfo("missing.dacpac"),
                TargetDatabaseName = "MyDb",
                TargetServerName = "localhost",
                Property = ["AllowDropBlockingAssemblies=Nope"],
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.DeployDacpac(options);

                    result.ShouldBe(1);
                    writer.ToString().ShouldContain("ERROR: An error occured while validating arguments:");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public void DeployDacpac_InvalidSqlCmdVar_ReturnsValidationError()
        {
            var options = new DeployOptions
            {
                Input = new FileInfo("missing.dacpac"),
                TargetDatabaseName = "MyDb",
                TargetServerName = "localhost",
                SqlCmdVar = ["InvalidSqlCmdVar"],
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.DeployDacpac(options);

                    result.ShouldBe(1);
                    writer.ToString().ShouldContain("ERROR: An error occured while validating arguments:");
                    writer.ToString().ShouldContain("Expected NAME=VALUE format for --sqlcmdvar");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public void DeployDacpac_WithPortAndSqlAuthentication_ReturnsValidationErrorForMissingPackage()
        {
            var options = new DeployOptions
            {
                Input = new FileInfo("missing.dacpac"),
                TargetDatabaseName = "MyDb",
                TargetServerName = "localhost",
                TargetPort = 1433,
                TargetUser = "sa",
                TargetPassword = "Password123!",
            };

            using var writer = new StringWriter();
            var originalOut = Console.Out;

            lock (ConsoleLock)
            {
                try
                {
                    Console.SetOut(writer);

                    var result = Program.DeployDacpac(options);

                    result.ShouldBe(1);
                    writer.ToString().ShouldContain("ERROR: An error occured while validating arguments:");
                    writer.ToString().ShouldContain("does not exist");
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
        }

        [TestMethod]
        public async Task DeployDacpac_WhenDeploymentFails_ReturnsOne()
        {
            var outputPath = new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.dacpac"));
            var inputList = CreateInputListFile("../../../../TestProject/Tables/MyTable.sql");

            try
            {
                var buildOptions = new BuildOptions
                {
                    Name = "DeploySource",
                    Version = "1.0.0.0",
                    Output = outputPath,
                    InputFile = inputList,
                };

                (await Program.BuildDacpac(buildOptions)).ShouldBe(0);

                var deployOptions = new DeployOptions
                {
                    Input = outputPath,
                    TargetServerName = "localhost",
                    TargetPort = 1,
                    TargetDatabaseName = "MyDb",
                };

                using var writer = new StringWriter();
                var originalOut = Console.Out;

                lock (ConsoleLock)
                {
                    try
                    {
                        Console.SetOut(writer);

                        var result = Program.DeployDacpac(deployOptions);

                        result.ShouldBe(1);
                    }
                    finally
                    {
                        Console.SetOut(originalOut);
                    }
                }
            }
            finally
            {
                if (outputPath.Exists)
                {
                    outputPath.Delete();
                }

                if (inputList.Exists)
                {
                    inputList.Delete();
                }
            }
        }

        private static FileInfo CreateInputListFile(string relativeInputPath)
        {
            var inputFile = new FileInfo(relativeInputPath);
            var listFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
            File.WriteAllText(listFilePath, inputFile.FullName + Environment.NewLine);
            return new FileInfo(listFilePath);
        }
    }
}
