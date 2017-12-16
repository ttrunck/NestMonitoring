FROM microsoft/dotnet:2.0-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /src
COPY *.sln ./
COPY NestMonitoringConsole/NestMonitoringConsole.csproj NestMonitoringConsole/
RUN dotnet restore
COPY . .
WORKDIR /src/NestMonitoringConsole
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base AS final
EXPOSE 8000
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "NestMonitoringConsole.dll"]
