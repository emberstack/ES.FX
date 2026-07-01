# Build from the repository root:
#   docker build -f src/ES.FX.Zendesk.MCP.Host/Dockerfile -t es-mcp-zendesk .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN dotnet restore "src/ES.FX.Zendesk.MCP.Host/ES.FX.Zendesk.MCP.Host.csproj"
RUN dotnet publish "src/ES.FX.Zendesk.MCP.Host/ES.FX.Zendesk.MCP.Host.csproj" \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ES.FX.Zendesk.MCP.Host.dll"]
