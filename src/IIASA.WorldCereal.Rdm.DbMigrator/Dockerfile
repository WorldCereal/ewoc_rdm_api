#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["src/IIASA.WorldCereal.Rdm.DbMigrator/IIASA.WorldCereal.Rdm.DbMigrator.csproj", "src/IIASA.WorldCereal.Rdm.DbMigrator/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/IIASA.WorldCereal.Rdm.EntityFrameworkCore.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain/IIASA.WorldCereal.Rdm.Domain.csproj", "src/IIASA.WorldCereal.Rdm.Domain/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain.Shared/IIASA.WorldCereal.Rdm.Domain.Shared.csproj", "src/IIASA.WorldCereal.Rdm.Domain.Shared/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application.Contracts/IIASA.WorldCereal.Rdm.Application.Contracts.csproj", "src/IIASA.WorldCereal.Rdm.Application.Contracts/"]
RUN dotnet restore "src/IIASA.WorldCereal.Rdm.DbMigrator/IIASA.WorldCereal.Rdm.DbMigrator.csproj"
COPY . .
WORKDIR "/src/src/IIASA.WorldCereal.Rdm.DbMigrator"
RUN dotnet build "IIASA.WorldCereal.Rdm.DbMigrator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IIASA.WorldCereal.Rdm.DbMigrator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IIASA.WorldCereal.Rdm.DbMigrator.dll"]