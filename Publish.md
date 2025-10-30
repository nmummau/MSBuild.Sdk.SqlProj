# How to use PublishContainer

from MSBuild.Sdk.SqlProj\src\MSBuild.Sdk.SqlProj

```sh
dotnet build -c Release &&
dotnet pack MSBuild.Sdk.SqlProj.csproj -c Release
```

Then copy the nupkg to a project you want to test with:
`C:\code\MSBuild.Sdk.SqlProj\src\MSBuild.Sdk.SqlProj\bin\Release\MSBuild.Sdk.SqlProj.3.3.0-beta.16.g87c1e17ddb.nupkg`

For me I move this nupkg to a directory I use as a local NuGet feed
`C:\LocalFeed`

Then in the project I want to use this, for me this is `c/code/wes/inventory/src`
```sh
dotnet nuget locals all --clear
```

Then build with the PublishContainer target
```sh
dotnet build ./WES.Inventory.Database/WES.Inventory.Database.csproj \
-t:PublishContainer \
-c Release \
-v:m
```

Due to the name of the project I'm using this with my resulting docker image is named `wes.inventory.database-publisher:release`

You can look into the files within the image
```sh
MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL="*" \
docker run --rm --entrypoint sh wes.inventory.database-publisher:release \
-lc "ls -l /work"
```

Finally you can run the image (in git bash)
```sh
MSYS_NO_PATHCONV=1 \
docker run --rm wes.inventory.database-publisher:release \
-TargetServerName:host.docker.internal,14333 \
-TargetDatabaseName:WES_001_inv \
-TargetUser:sa \
-TargetPassword:YourPassword123! \
-TargetTrustServerCertificate:true \
"/p:DropObjectsNotInSource=True" "/p:BlockOnPossibleDataLoss=False"
```

Due to the way the entrypoint is setup, it automatically runs sqlpackage Publish with your dacpac


Pre-requisite:
Run a SQL Server instance in a container. Do this before docker run.
```sh
docker run -d --name sql2022 \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=YourPassword123!" \
  -p 14333:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

other tools
task kill
```
taskkill //F //IM msbuild.exe
taskkill //F //IM dotnet.exe
```