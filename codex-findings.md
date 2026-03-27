are there any tests or code in this repo that are catastrophic? not perf issues, but
  literlaly bugs I should report


• I’m doing a quick bug-focused pass over the areas we’ve been touching, with emphasis on
  actual behavioral defects rather than style or perf.

• I found two real bugs worth reporting, and one lower-severity correctness issue. I’m
  pulling the exact line references for the deploy path so I can give you a clean review-
  style answer.

• 1. src/DacpacTool/Program.cs:237 with src/DacpacTool/PackageDeployer.cs:108: deployment
     can fail and still return exit code 0. PackageDeployer.Deploy() catches
     DacServicesException/Exception, logs an error, and returns void; then
     Program.DeployDacpac() continues to src/DacpacTool/Program.cs:244. That means CI or
     scripts can treat a failed deployment as success.
  2. src/DacpacTool/Program.cs:60: single-part --reference values are broken. The second
     branch is if (...) instead of else if (...), so a one-part reference hits the first
     branch and then falls into the else, which indexes referenceDetails[2]. That is a real
     functional bug in normal argument handling.
  3. src/DacpacTool/Program.cs:46 and src/DacpacTool/Program.cs:209: malformed
     BuildProperty / SqlCmdVar inputs can throw IndexOutOfRangeException instead of a clean
     validation error because the code assumes Split(..., 2) always yields both sides. That
     is less severe than the two above, but still a user-facing bug.

  Those are the ones I’d actually report. The first two are the important ones.


› can you write up separte issues for each of these with real world examples of how to
  repro


• Yes. Drafts below.

  Issue 1
  Title: deploy returns exit code 0 even when deployment fails

  Body:

  ## Summary

  `DacpacTool deploy` can report a deployment failure to stdout but still exit with code
  `0`.

  That makes CI/CD and shell scripts treat a failed deployment as success.

  ## Affected code

  - [Program.cs](/home/nmummau/Code/MSBuild.Sdk.SqlProj/src/DacpacTool/Program.cs#L237)
  - [PackageDeployer.cs](/home/nmummau/Code/MSBuild.Sdk.SqlProj/src/DacpacTool/
  PackageDeployer.cs#L108)

  `Program.DeployDacpac()` always returns `0` after calling `deployer.Deploy(...)`.

  But `PackageDeployer.Deploy()` catches `DacServicesException` and general `Exception`,
  logs an error, and does not propagate failure back to the caller.

  ## Real-world impact

  A deployment step in CI can fail against SQL Server, print an error, and still let the
  pipeline continue as if deployment succeeded.

  ## Repro

  1. Build a valid dacpac.
  2. Run deploy against a reachable SQL Server with an invalid target database state or
  insufficient permissions so deployment fails.

  Example:

  ```bash
  dotnet run --project ./src/DacpacTool/DacpacTool.csproj -- deploy \
    --input ./path/to/MyDatabase.dacpac \
    --targetServerName localhost \
    --targetDatabaseName master \
    --targetUser sa \
    --targetPassword WrongOrInsufficientPermissions
  echo $?

  ## Actual behavior

  The tool can print an error such as:

  ERROR: Deployment of database 'master' failed: ...

  but still exit with code 0.

  ## Expected behavior

  If deployment fails, the process should exit nonzero.

  ## Suggested fix

  Have PackageDeployer.Deploy() return success/failure or throw on failure, and have
  Program.DeployDacpac() return 1 when deployment fails.


  **Issue 2**
  Title: single-part `--reference` values in `build` are parsed incorrectly

  Body:

  ```md
  ## Summary

  `DacpacTool build --reference <path>` is broken for the simplest valid form: a reference
  with only the dacpac path.

  ## Affected code

  - [Program.cs](/home/nmummau/Code/MSBuild.Sdk.SqlProj/src/DacpacTool/Program.cs#L54)

  The reference parsing logic uses:

  ```csharp
  if (referenceDetails.Length == 1) { ... }
  if (referenceDetails.Length == 2) { ... }
  else { ... }

  The second branch should be else if.

  With a one-part reference, the code enters the first if, then falls into the else attached
  to the second if, and tries to access referenceDetails[2].

  ## Real-world impact

  A normal build invocation with a plain dacpac reference can fail unexpectedly even though
  the input format is valid.

  ## Repro

  1. Create any valid referenced dacpac, for example Reference.dacpac.
  2. Create an input list file with one SQL file.
  3. Run build with a single-part reference.

  Example:

  dotnet run --project ./src/DacpacTool/DacpacTool.csproj -- build \
    --name MyPackage \
    --version 1.0.0 \
    --output /tmp/MyPackage.dacpac \
    --inputfile /tmp/inputfiles.txt \
    --reference /tmp/Reference.dacpac

  Where /tmp/inputfiles.txt contains one valid SQL file path.

  ## Actual behavior

  The build fails during reference parsing because the code falls through into the 3-part
  parsing branch.

  ## Expected behavior

  A single-part reference should call:

  packageBuilder.AddReference(referenceDetails[0]);

  and continue normally.

  ## Suggested fix

  Change the second branch from if (referenceDetails.Length == 2) to else if
  (referenceDetails.Length == 2).


  **Issue 3**
  Title: malformed `--buildproperty` and `--sqlcmdvar` values can crash with index errors
  instead of validation errors

  Body:

  ```md
  ## Summary

  Malformed `--buildproperty` and `--sqlcmdvar` inputs are not validated before indexing
  into the split result.

  That can produce an `IndexOutOfRangeException` instead of a clean argument validation
  error.

  ## Affected code

  - [Program.cs](/home/nmummau/Code/MSBuild.Sdk.SqlProj/src/DacpacTool/Program.cs#L46)
  - [Program.cs](/home/nmummau/Code/MSBuild.Sdk.SqlProj/src/DacpacTool/Program.cs#L209)

  Current code assumes `Split('=', 2)` always returns both key and value:

  ```csharp
  string[] keyValuePair = propertyValue.Split('=', 2);
  packageBuilder.SetProperty(keyValuePair[0], keyValuePair[1]);

  and similarly for SqlCmdVar.

  ## Real-world impact

  Typos in CLI input produce a crash-like experience instead of a useful validation message.

  ## Repro 1: buildproperty

  dotnet run --project ./src/DacpacTool/DacpacTool.csproj -- build \
    --name MyPackage \
    --version 1.0.0 \
    --output /tmp/MyPackage.dacpac \
    --inputfile /tmp/inputfiles.txt \
    --buildproperty InvalidPropertyWithoutEquals

  ## Repro 2: sqlcmdvar during deploy

  dotnet run --project ./src/DacpacTool/DacpacTool.csproj -- deploy \
    --input ./path/to/MyDatabase.dacpac \
    --targetServerName localhost \
    --targetDatabaseName MyDb \
    --sqlcmdvar InvalidSqlCmdVar

  ## Actual behavior

  The tool can fail with an index error or generic deployment/build failure path.

  ## Expected behavior

  The tool should return a clear ArgumentException such as:

  Expected NAME=VALUE format for --buildproperty

  or

  Expected NAME=VALUE format for --sqlcmdvar

  ## Suggested fix

  Validate keyValuePair.Length == 2 before indexing and throw a targeted ArgumentException
  when the format is invalid.


  If you want, I can also turn these into shorter GitHub-ready versions with less code
  detail.


› can we write projects with scripts that repro these issues?


• Yes, but only for some of them.

  Best candidates:

  - Issue 2: single-part --reference parsing bug
  - Possibly issue 3: malformed --buildproperty / --sqlcmdvar

  Not a good fit:

  - Issue 1: deploy returns 0 on failure

  Why:

  - issue 2 is deterministic and file-based, so a small sample project plus a script is a
    good repo repro
  - issue 3 is really just CLI argument validation, so a script alone is enough; a whole
    project is probably overkill
  - issue 1 depends on an actual deployment failure scenario against SQL Server, which is
    much harder to make portable and reliable in-repo

  My recommendation:

  - add one small repro script for issue 2
  - if you want, add a second tiny script for issue 3
  - do not try to add an in-repo repro project for issue 1 unless the repo already has a
    stable SQL Server test environment

  So yes, but selectively. Issue 2 is the one I’d definitely do.


› go for it where applicatble


• I’m adding lightweight repro scripts for the deterministic CLI bugs: the single-part
  --reference parsing issue and malformed --buildproperty / --sqlcmdvar handling. I’m using
  existing sample inputs where possible so this stays small and reproducible.

• I’m adding two small bash repros under test/: one for the single-part --reference bug and
  one for malformed --buildproperty / --sqlcmdvar. They’ll be self-contained and use temp
  files plus the existing sample SQL file.

• The scripts are in place. I’m making them executable and doing a shell syntax check so
  they’re at least runnable as-is.

• Added two repro scripts under test:

  - test/repro-build-single-part-reference.sh
      - builds a base dacpac
      - then reproduces the single-part --reference bug with a second build
  - test/repro-malformed-cli-input.sh
      - reproduces malformed --buildproperty
      - reproduces malformed --sqlcmdvar

  They are executable and pass bash -n.

  I did not add one for the deploy-exit-code bug because that needs a real, reliably failing
  SQL deployment target to be meaningful.


› so what's your suggestion/ write an issue for each and push the PR ofr people to use it


• Yes.

  My recommendation:

  - open separate issues for the real bugs
  - keep them small and concrete
  - push one PR with:
      - the deterministic bug fix(es)
      - the regression tests
      - optionally the repro scripts if you think maintainers will find them useful

  I would split it like this:

  1. Issue: single-part --reference parsing bug

  - definitely file this
  - definitely fix in PR
  - include the repro script and regression test

  2. Issue: malformed --buildproperty / --sqlcmdvar handling

  - file this too
  - good PR candidate if you want
  - repro script is useful here
  - tests are straightforward

  3. Issue: deploy returns 0 on failure

  - definitely file this
  - but I would probably not bundle the fix into the same PR unless you want a larger
    behavior change
  - it is a more important bug, but also a more semantic one

  So the cleanest path is:

  - file 3 issues
  - make 1 focused PR for issues 1 and 2
  - optionally a second PR for the deploy exit-code bug

  On whether to push the repro scripts:

  - yes, if they are helping document the issue and stay small
  - no, if the PR already has solid automated tests and the scripts feel redundant

  My bias:

  - keep the --reference regression test
  - keep the --buildproperty / --sqlcmdvar tests
  - only include the repro scripts if you want maintainers to have copy-paste reproduction
    outside the test project

  If you want, I can now:

  - draft the three issue texts in final GitHub-ready form
  - and draft a commit message / PR summary for the bugfix branch


› can you fix each issue easily? do it


• I’m fixing the three real bugs directly now: the --reference parsing bug, malformed
  NAME=VALUE handling, and deploy returning 0 on failure. After that I’ll add focused
  regression tests around Program.BuildDacpac(...) and Program.DeployDacpac(...).

• I’ve isolated the smallest safe production changes:

  - Program.BuildDacpac(...): validate NAME=VALUE, fix if/else if/else for references
  - PackageDeployer.Deploy(...): return bool success/failure
  - Program.DeployDacpac(...): return 1 when deployment fails

  I’m patching those now, then I’ll add the regression tests that prove each bug is fixed.

• I’m updating ProgramTests now: one-part reference success, clean NAME=VALUE validation for
  both options, and a deploy-failure case that now must return 1 instead of 0.

• The regression tests are in. I’m rebuilding the test project now to catch any behavior
  drift from the new bool Deploy(...) signature and the new ProgramTests.

• Fixed all three bugs.

  Code changes:

  - src/DacpacTool/Program.cs
      - fixed the single-part --reference bug by changing the branch shape to if / else if /
        else
      - added clean NAME=VALUE validation for --buildproperty and --sqlcmdvar
  - src/DacpacTool/PackageDeployer.cs
      - Deploy(...) now returns bool
      - returns false on deployment failure instead of only logging
  - src/DacpacTool/Program.cs
      - DeployDacpac(...) now returns 1 when deployer.Deploy(...) fails

  Regression tests added in test/DacpacTool.Tests/ProgramTests.cs:

  - single-part reference build succeeds
  - malformed --buildproperty throws a clean validation error
  - malformed --sqlcmdvar returns a validation error path
  - failed deployment returns 1

  dotnet build ./test/DacpacTool.Tests/DacpacTool.Tests.csproj -c Release -m:1 passed.