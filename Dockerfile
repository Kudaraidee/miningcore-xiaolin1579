FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy as BUILDER
WORKDIR /app 
RUN apt-get update && \
    apt-get -y install \
    cmake build-essential libssl-dev \
    pkg-config libboost-all-dev \
    libsodium-dev libzmq5 libzmq3-dev libgmp-dev

COPY . .

WORKDIR /app/src/Miningcore
RUN dotnet publish -c Release --framework net6.0 -o ../../build

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-jammy

WORKDIR /app

RUN apt-get update && \
    apt-get install -y libzmq5 libzmq3-dev libsodium-dev libgmp-dev libboost-all-dev curl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*
RUN groupadd -g 10001 miningcore
RUN useradd -u 10001 -g miningcore -d /app miningcore
RUN chown -R miningcore:miningcore /app
USER miningcore

COPY --from=BUILDER /app/build ./

CMD ["./Miningcore", "-c", "config.json" ]