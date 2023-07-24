#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["tools/ConsortiumDbUpdate/ConsortiumDbUpdate.csproj", "tools/ConsortiumDbUpdate/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application.Contracts/IIASA.WorldCereal.Rdm.Application.Contracts.csproj", "src/IIASA.WorldCereal.Rdm.Application.Contracts/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain.Shared/IIASA.WorldCereal.Rdm.Domain.Shared.csproj", "src/IIASA.WorldCereal.Rdm.Domain.Shared/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore.DbMigrations/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/IIASA.WorldCereal.Rdm.EntityFrameworkCore.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain/IIASA.WorldCereal.Rdm.Domain.csproj", "src/IIASA.WorldCereal.Rdm.Domain/"]
COPY ["src/IIASA.WorldCereal.Rdm.ExcelOps/IIASA.WorldCereal.Rdm.ExcelOps.csproj", "src/IIASA.WorldCereal.Rdm.ExcelOps/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application/IIASA.WorldCereal.Rdm.Application.csproj", "src/IIASA.WorldCereal.Rdm.Application/"]
RUN dotnet restore "tools/ConsortiumDbUpdate/ConsortiumDbUpdate.csproj"
COPY . .
WORKDIR "/src/tools/ConsortiumDbUpdate"
RUN dotnet build "ConsortiumDbUpdate.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConsortiumDbUpdate.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsortiumDbUpdate.dll"]