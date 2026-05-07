FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY global.json global.json
COPY nuget.config nuget.config

COPY src/OpenIdentityStack.Domain/OpenIdentityStack.Domain.csproj src/OpenIdentityStack.Domain/
COPY src/OpenIdentityStack.Application/OpenIdentityStack.Application.csproj src/OpenIdentityStack.Application/
COPY src/OpenIdentityStack.Infrastructure/OpenIdentityStack.Infrastructure.csproj src/OpenIdentityStack.Infrastructure/
COPY src/OpenIdentityStack.DbMigrator/OpenIdentityStack.DbMigrator.csproj src/OpenIdentityStack.DbMigrator/

RUN dotnet restore src/OpenIdentityStack.DbMigrator/OpenIdentityStack.DbMigrator.csproj

COPY src/OpenIdentityStack.Domain/ src/OpenIdentityStack.Domain/
COPY src/OpenIdentityStack.Application/ src/OpenIdentityStack.Application/
COPY src/OpenIdentityStack.Infrastructure/ src/OpenIdentityStack.Infrastructure/
COPY src/OpenIdentityStack.DbMigrator/ src/OpenIdentityStack.DbMigrator/

RUN dotnet publish src/OpenIdentityStack.DbMigrator/OpenIdentityStack.DbMigrator.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OpenIdentityStack.DbMigrator.dll"]