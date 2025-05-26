FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["tools/ConsortiumDbUpdate/ConsortiumDbUpdate.csproj", "tools/ConsortiumDbUpdate/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application.Contracts/IIASA.WorldCereal.Rdm.Application.Contracts.csproj", "src/IIASA.WorldCereal.Rdm.Application.Contracts/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain.Shared/IIASA.WorldCereal.Rdm.Domain.Shared.csproj", "src/IIASA.WorldCereal.Rdm.Domain.Shared/"]
COPY ["src/IIASA.WorldCereal.Rdm.Application/IIASA.WorldCereal.Rdm.Application.csproj", "src/IIASA.WorldCereal.Rdm.Application/"]
COPY ["src/IIASA.WorldCereal.Rdm.Domain/IIASA.WorldCereal.Rdm.Domain.csproj", "src/IIASA.WorldCereal.Rdm.Domain/"]
COPY ["src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/IIASA.WorldCereal.Rdm.EntityFrameworkCore.csproj", "src/IIASA.WorldCereal.Rdm.EntityFrameworkCore/"]
COPY ["src/IIASA.WorldCereal.Rdm.ExcelOps/IIASA.WorldCereal.Rdm.ExcelOps.csproj", "src/IIASA.WorldCereal.Rdm.ExcelOps/"]
RUN dotnet restore "tools/ConsortiumDbUpdate/ConsortiumDbUpdate.csproj"
COPY . .
WORKDIR "/src/tools/ConsortiumDbUpdate"
RUN dotnet build "ConsortiumDbUpdate.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ConsortiumDbUpdate.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsortiumDbUpdate.dll"]
