FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0-aot

WORKDIR /src

COPY BoothDownloader/ ./BoothDownloader/

WORKDIR /src/BoothDownloader

RUN dotnet publish BoothDownloader.csproj \
    -c "Release Linux" \
    -r linux-x64 \
    -p:PublishAot=true \
    --self-contained true \
    -o /out
