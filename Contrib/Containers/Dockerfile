FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Packages.props", "NuGet.Config", "global.json", "./"]
COPY ["GingerCommon/GingerCommon.csproj", "GingerCommon/packages.lock.json", "GingerCommon/"]
COPY ["WalletWasabi.Backend/WalletWasabi.Backend.csproj", "WalletWasabi.Backend/"]
COPY ["WalletWasabi/WalletWasabi.csproj", "WalletWasabi/packages.lock.json", "WalletWasabi/"]
RUN dotnet restore "WalletWasabi.Backend/WalletWasabi.Backend.csproj" --locked-mode
COPY . .
WORKDIR "/src/WalletWasabi.Backend"
RUN dotnet build "WalletWasabi.Backend.csproj" --no-restore -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WalletWasabi.Backend.csproj" --no-restore -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WalletWasabi.Backend.dll"]
