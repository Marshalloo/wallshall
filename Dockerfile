FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Wallshall.csproj .
RUN dotnet restore -r win-x64 -p:EnableWindowsTargeting=true
COPY . .
RUN dotnet publish -c Release -r win-x64 --no-restore \
    -p:EnableWindowsTargeting=true \
    -p:PublishSingleFile=true \
    --self-contained false \
    -o /out

# В итоговом "образе" только готовые файлы — их выгружаем на диск
FROM scratch
COPY --from=build /out /
