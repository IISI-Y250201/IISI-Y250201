# 請參閱 https://aka.ms/customizecontainer 了解如何自訂您的偵錯容器，以及 Visual Studio 如何使用此 Dockerfile 來組建您的映像，以加快偵錯速度。

# 此階段用於以快速模式從 VS 執行時 (偵錯設定的預設值)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# 此階段是用來組建服務專案
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ./NuGet.Config ./NuGet.Config
COPY libsource/ENVCC0001N01 libsource/ENVCC0001N01
COPY libsource/ICMCC0001N01 libsource/ICMCC0001N01
COPY libsource/MEDCC0001N01 libsource/MEDCC0001N01
COPY libsource/RPTCC0001N01 libsource/RPTCC0001N01
COPY example/IHANC1052R01 example/IHANC1052R01
RUN dotnet restore example/IHANC1052R01/IHANC1052R01.csproj --configfile ./NuGet.Config
COPY . .
WORKDIR "/src/."
RUN dotnet build example/IHANC1052R01/IHANC1052R01.csproj -c $BUILD_CONFIGURATION -o /app/build

# 此階段可用來發佈要複製到最終階段的服務專案
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish example/IHANC1052R01/IHANC1052R01.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此階段用於生產環境，或以一般模式從 VS 執行時 (未使用偵錯設定時的預設值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IHANC1052R01.dll"]