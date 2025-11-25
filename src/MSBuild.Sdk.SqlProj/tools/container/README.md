# Container Publishing for SQL Database Projects

This feature allows you to publish your SQL database project as a container image that includes both your DACPAC and SqlPackage, making it easy to deploy databases in containerized environments.

## Features

- **No Docker required**: Uses .NET SDK's built-in container support
- **Self-contained**: Image includes SqlPackage and your DACPAC
- **Publish profile support**: Can use `.publish.xml` files
- **Simple usage**: Just run the container with connection parameters

## Quick Start

### Build a container image

```bash
dotnet build YourDatabase.sqlproj -t:PublishContainer -c Release
```

This creates a container image named `yourdatabase-publisher:release`

### Run the container to publish

```bash
docker run --rm yourdatabase-publisher:release \
  /TargetServerName:localhost \
  /TargetDatabaseName:MyDatabase \
  /TargetUser:sa \
  /TargetPassword:YourPassword
```

## Configuration

You can customize the container image name and tag:

```bash
dotnet build YourDatabase.sqlproj -t:PublishContainer \
  -p:ContainerImageName=myapp-db \
  -p:ContainerImageTag=v1.0.0
```

## Using Publish Profiles

Include your `.publish.xml` file in your project:

```xml
<Project Sdk="MSBuild.Sdk.SqlProj/3.3.0">
  <ItemGroup>
    <Content Include="Production.publish.xml" />
  </ItemGroup>
</Project>
```

The profile will be included in the container at `/work/`. Reference it when running:

```bash
docker run --rm yourdatabase-publisher:release \
  /Profile:Production.publish.xml \
  /TargetServerName:prod-server
```

## Advanced Usage

### Custom SqlPackage Parameters

Pass any SqlPackage parameters directly:

```bash
docker run --rm yourdatabase-publisher:release \
  /TargetServerName:localhost \
  /TargetDatabaseName:MyDatabase \
  /p:DropObjectsNotInSource=True \
  /p:BlockOnPossibleDataLoss=False
```

### Including Referenced DACPACs

Referenced DACPACs (from PackageReference or ProjectReference) are automatically included in the container image at `/work/`.

### CI/CD Integration

Example GitHub Actions workflow:

```yaml
- name: Build and push database container
  run: |
    dotnet build MyDatabase.sqlproj -t:PublishContainer -c Release \
      -p:ContainerImageName=myregistry.azurecr.io/myapp-db \
      -p:ContainerImageTag=${{ github.sha }}
    docker push myregistry.azurecr.io/myapp-db:${{ github.sha }}
```

## How It Works

1. **PrepareContainerDacpac** target builds your project and stages the DACPAC(s) to `.container/`
2. **PublishContainer** target uses .NET SDK's container support to:
   - Install SqlPackage as a dotnet tool
   - Copy your DACPAC(s) into the image
   - Create an entrypoint that runs SqlPackage with your parameters

## Requirements

- .NET 8.0 SDK or later (for container support)
- MSBuild.Sdk.SqlProj 3.3.0 or later

## Troubleshooting

### "No .dacpac files found"

Ensure your project builds successfully before running PublishContainer.

### "SqlPackage not found"

Check that SqlPackageVersion is set correctly in your project or SDK.

### Multiple DACPACs

If you have multiple DACPACs in your image, set the `DACPAC_NAME` environment variable:

```bash
docker run --rm -e DACPAC_NAME=MyDatabase.dacpac yourdatabase-publisher:release ...
```
