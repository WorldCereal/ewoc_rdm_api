#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG DOTNET_USE_POLLING_FILE_WATCHER=1
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NuGet.Config", "."]
COPY ["src/IIASA.WorldCereal.Rdm.HttpApi.Host/IIASA.WorldCereal.Rdm.HttpApi.Host.csproj", "src/IIASA.WorldCereal.Rdm.HttpApi.Host/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application/IIASA.WorldCereal.Rdm.Application.csproj", "src/IIASA.WorldCereal.Rdm.Application/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain/IIASA.WorldCereal.Rdm.Domain.csproj", "src/IIASA.WorldCereal.Rdm.Domain/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain.Shared/IIASA.WorldCereal.Rdm.Domain.Shared.csproj", "src/IIASA.WorldCereal.Rdm.Domain.Shared/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application.Contracts/IIASA.WorldCereal.Rdm.Application.Contracts.csproj", "src/IIASA.WorldCereal.Rdm.Application.Contracts/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/IIASA.WorldCereal.Rdm.EntityFrameworkCore.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/"]
COPY ["src/IIASA.WorldCereal.Rdm.ExcelOps/IIASA.WorldCereal.Rdm.ExcelOps.csproj", "src/IIASA.WorldCereal.Rdm.ExcelOps/"]
COPY ["src/IIASA.WorldCereal.Rdm.HttpApi/IIASA.WorldCereal.Rdm.HttpApi.csproj", "src/IIASA.WorldCereal.Rdm.HttpApi/"]
RUN dotnet restore "./src/IIASA.WorldCereal.Rdm.HttpApi.Host/./IIASA.WorldCereal.Rdm.HttpApi.Host.csproj"
COPY . .
WORKDIR "/src/src/IIASA.WorldCereal.Rdm.HttpApi.Host"
RUN dotnet build "./IIASA.WorldCereal.Rdm.HttpApi.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./IIASA.WorldCereal.Rdm.HttpApi.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IIASA.WorldCereal.Rdm.HttpApi.Host.dll"]