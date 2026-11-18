# KnockRush dedicated server container (Linux, headless Unity UNITY_SERVER build).
#
# Build the server first:   Tools/unity.sh server   (output: KnockRush/Builds/LinuxServer/)
# Then from the repository root:
#   docker build -f Server/Dockerfile -t knockrush-server:0.4 .
#   docker run --rm -p 7777:7777/udp -e KNOCKRUSH_PHOTON_APPID=<id> knockrush-server:0.4 -session local-01 -port 7777
#
# Edgegap: push the image to your registry, create an app version with a UDP port named
# "gameport" (7777) and the KNOCKRUSH_PHOTON_APPID environment variable. Edgegap injects
# ARBITRIUM_PUBLIC_IP / ARBITRIUM_PORT_GAMEPORT_* which the server reads automatically.

FROM ubuntu:22.04

RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates libglib2.0-0 \
 && rm -rf /var/lib/apt/lists/* \
 && useradd --create-home --shell /usr/sbin/nologin knockrush

WORKDIR /app
COPY --chown=knockrush:knockrush KnockRush/Builds/LinuxServer/ /app/
RUN chmod +x /app/KnockRushServer.x86_64

USER knockrush
EXPOSE 7777/udp

ENTRYPOINT ["/app/KnockRushServer.x86_64", "-batchmode", "-nographics", "-server", "-logFile", "/dev/stdout"]
CMD ["-session", "knockrush-01", "-port", "7777", "-maxPlayers", "32"]
