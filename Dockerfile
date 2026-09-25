# Сборка Windows-приложения в обычном Linux-контейнере
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Wallshall/Wallshall.csproj src/Wallshall/
RUN dotnet restore src/Wallshall/Wallshall.csproj -r win-x64 -p:EnableWindowsTargeting=true

COPY . .
RUN dotnet publish src/Wallshall/Wallshall.csproj -c Release -r win-x64 --no-restore \
    -p:EnableWindowsTargeting=true \
    -p:PublishSingleFile=true \
    --self-contained false \
    -o /out

# В итоговом "образе" только готовый .exe — его выгружаем на диск
FROM scratch
COPY --from=build /out /
