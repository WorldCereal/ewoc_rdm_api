#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG DOTNET_USE_POLLING_FILE_WATCHER=1
WORKDIR /src
COPY ["NuGet.Config", "."]
COPY ["src/IIASA.WorldCereal.Rdm.DbMigrator/IIASA.WorldCereal.Rdm.DbMigrator.csproj", "src/IIASA.WorldCereal.Rdm.DbMigrator/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application.Contracts/IIASA.WorldCereal.Rdm.Application.Contracts.csproj", "src/IIASA.WorldCereal.Rdm.Application.Contracts/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain.Shared/IIASA.WorldCereal.Rdm.Domain.Shared.csproj", "src/IIASA.WorldCereal.Rdm.Domain.Shared/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/IIASA.WorldCereal.Rdm.EntityFrameworkCore.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain/IIASA.WorldCereal.Rdm.Domain.csproj", "src/IIASA.WorldCereal.Rdm.Domain/"]
RUN dotnet restore "./src/IIASA.WorldCereal.Rdm.DbMigrator/./IIASA.WorldCereal.Rdm.DbMigrator.csproj"
COPY . .
WORKDIR "/src/src/IIASA.WorldCereal.Rdm.DbMigrator"
RUN dotnet build "./IIASA.WorldCereal.Rdm.DbMigrator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG DOTNET_USE_POLLING_FILE_WATCHER=1
RUN dotnet publish "./IIASA.WorldCereal.Rdm.DbMigrator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
ARG DOTNET_USE_POLLING_FILE_WATCHER=1
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IIASA.WorldCereal.Rdm.DbMigrator.dll"]