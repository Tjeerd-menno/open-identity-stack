ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Build.props
COPY Directory.Packages.props Directory.Packages.props
COPY global.json global.json
COPY nuget.config nuget.config

COPY src/OpenIdentityStack.Domain/OpenIdentityStack.Domain.csproj src/OpenIdentityStack.Domain/
COPY src/SharedKernel/SharedKernel.csproj src/SharedKernel/
COPY src/OpenIdentityStack.Application/OpenIdentityStack.Application.csproj src/OpenIdentityStack.Application/
COPY src/OpenIdentityStack.Infrastructure/OpenIdentityStack.Infrastructure.csproj src/OpenIdentityStack.Infrastructure/
COPY src/OpenIdentityStack.ServiceDefaults/OpenIdentityStack.ServiceDefaults.csproj src/OpenIdentityStack.ServiceDefaults/
COPY src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj src/OpenIdentityStack.Api/

RUN dotnet restore src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj

COPY src/OpenIdentityStack.Domain/ src/OpenIdentityStack.Domain/
COPY src/SharedKernel/ src/SharedKernel/
COPY src/OpenIdentityStack.Application/ src/OpenIdentityStack.Application/
COPY src/OpenIdentityStack.Infrastructure/ src/OpenIdentityStack.Infrastructure/
COPY src/OpenIdentityStack.ServiceDefaults/ src/OpenIdentityStack.ServiceDefaults/
COPY src/OpenIdentityStack.Api/ src/OpenIdentityStack.Api/

RUN dotnet publish src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM build AS native-aot-build
RUN dotnet publish src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o /app/native-aot \
    /p:IsAotPublish=true \
    /p:PublishAot=true

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS native-aot
WORKDIR /app

COPY --chown=app:app --from=native-aot-build /app/native-aot .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["./OpenIdentityStack.Api"]

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --chown=root:root --chmod=555 --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER app
ENTRYPOINT ["dotnet", "OpenIdentityStack.Api.dll"]
