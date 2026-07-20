# Build from the repository root:
#   docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-fx-mcp-zendesk .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the restore graph (props + the project closure) first, so the restored-packages
# layer is reused until a project file or a pinned package version actually changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ES.FX.Additions.Microsoft.AspNetCore/ES.FX.Additions.Microsoft.AspNetCore.csproj src/ES.FX.Additions.Microsoft.AspNetCore/
COPY src/ES.FX.Additions.Serilog/ES.FX.Additions.Serilog.csproj src/ES.FX.Additions.Serilog/
COPY src/ES.FX.Hosting/ES.FX.Hosting.csproj src/ES.FX.Hosting/
COPY src/ES.FX.Ignite/ES.FX.Ignite.csproj src/ES.FX.Ignite/
COPY src/ES.FX.Ignite.Serilog/ES.FX.Ignite.Serilog.csproj src/ES.FX.Ignite.Serilog/
COPY src/ES.FX.Ignite.Spark/ES.FX.Ignite.Spark.csproj src/ES.FX.Ignite.Spark/
COPY src/ES.FX.Ignite.Zendesk/ES.FX.Ignite.Zendesk.csproj src/ES.FX.Ignite.Zendesk/
COPY src/ES.FX.Zendesk/ES.FX.Zendesk.csproj src/ES.FX.Zendesk/
COPY src/ES.FX.Zendesk.MCP.Host/ES.FX.Zendesk.MCP.Host.csproj src/ES.FX.Zendesk.MCP.Host/
RUN dotnet restore "src/ES.FX.Zendesk.MCP.Host/ES.FX.Zendesk.MCP.Host.csproj"

# Now copy the sources; .dockerignore keeps bin/obj/.artifacts and dev secrets out of the context.
COPY . .
# GeneratePackageOnBuild=false: the referenced ES.FX.* libraries auto-pack on build by repo convention,
# which is pure waste inside an image build and requires pack assets (README.md) excluded from the context.
RUN dotnet publish "src/ES.FX.Zendesk.MCP.Host/ES.FX.Zendesk.MCP.Host.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ES.FX.Zendesk.MCP.Host.dll"]
